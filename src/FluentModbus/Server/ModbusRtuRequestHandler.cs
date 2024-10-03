using Microsoft.Extensions.Logging;

namespace FluentModbus;

internal class ModbusRtuRequestHandler : ModbusRequestHandler, IDisposable
{
    #region Fields

    private IModbusRtuSerialPort _serialPort;

    private readonly ILogger _logger;

    #endregion

    #region Constructors

    public ModbusRtuRequestHandler(IModbusRtuSerialPort serialPort, ModbusRtuServer rtuServer, ILogger logger) : base(rtuServer, 256)
    {
        _logger = logger;
        _serialPort = serialPort;
        _serialPort.Open();

        base.Start();
    }

    #endregion

    #region Properties

    public override string DisplayName => _serialPort.PortName;

    protected override bool IsResponseRequired => ModbusServer.UnitIdentifiers.Contains(UnitIdentifier);

    #endregion

    #region Methods

    internal override async Task ReceiveRequestAsync()
    {
        if (CancellationToken.IsCancellationRequested)
            return;

        IsReady = false;

        try
        {
            if (await TryReceiveRequestAsync())
            {
                IsReady = true; // WriteResponse() can be called only when IsReady = true

                if (ModbusServer.IsAsynchronous)
                    WriteResponse();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The connection will be closed");
            
            CancelToken();
        }
    }

    protected override int WriteFrame(Action extendFrame)
    {
        int frameLength;
        ushort crc;

        FrameBuffer.Writer.Seek(0, SeekOrigin.Begin);

        // add unit identifier
        FrameBuffer.Writer.Write(UnitIdentifier);

        // add PDU
        extendFrame();

        // add CRC
        frameLength = unchecked((int)FrameBuffer.Writer.BaseStream.Position);
        crc = ModbusUtils.CalculateCRC(FrameBuffer.Buffer.AsMemory(0, frameLength));
        FrameBuffer.Writer.Write(crc);

        return frameLength + 2;
    }

    protected override void OnResponseReady(int frameLength)
    {
        _serialPort.Write(FrameBuffer.Buffer, 0, frameLength);
    }

    private async Task<bool> TryReceiveRequestAsync()
    {
        // Whenever the serial port has a read timeout set, a TimeoutException might
        // occur which is catched immediately. The reason is that - opposed to the TCP
        // server - the RTU server maintains only a single connection and if that 
        // connection is closed, there would be no point in running that server anymore. 
        // To avoid that, the connection is kept alive by catching the TimeoutException.

        Length = 0;

        try
        {               
            while (true)
            {
                Length += await _serialPort.ReadAsync(FrameBuffer.Buffer, Length, FrameBuffer.Buffer.Length - Length, CancellationToken);

                // full frame received
                if (ModbusUtils.DetectRequestFrame(255, FrameBuffer.Buffer.AsMemory(0, Length)))
                {
                    FrameBuffer.Reader.BaseStream.Seek(0, SeekOrigin.Begin);

                    // read unit identifier
                    UnitIdentifier = FrameBuffer.Reader.ReadByte();

                    break;
                }
                else
                {
                    // reset length because one or more chunks of data were received and written to
                    // the buffer, but no valid Modbus frame could be detected and now the buffer is full
                    if (Length == FrameBuffer.Buffer.Length)
                        Length = 0;
                }
            }
        }
        catch (TimeoutException)
        {
            return false;
        }

        // make sure that the incoming frame is actually adressed to this server
        if (ModbusServer.UnitIdentifiers.Contains(UnitIdentifier))
        {
            LastRequest.Restart();
            return true;
        }
        
        else
        {
            return false;
        }
    }

    #endregion

    #region IDisposable Support

    private bool _disposedValue = false;

    protected override void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
                _serialPort.Close();

            _disposedValue = true;
        }

        base.Dispose(disposing);
    }

    #endregion
}