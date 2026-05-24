using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace FluentModbus;

public class ModbusRtuOverTcpRequestHandler : ModbusRequestHandler, IDisposable
{
    #region Fields

    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _networkStream;

    private ushort _transactionIdentifier;
    private ushort _protocolIdentifier;
    private ushort _bytesFollowing;

    private readonly ILogger _logger;

    #endregion

    #region Constructors

    public ModbusRtuOverTcpRequestHandler(TcpClient tcpClient, ModbusTcpServer tcpServer, ILogger logger)
        : base(tcpServer, 260)
    {
        _logger = logger;
        _tcpClient = tcpClient;
        _networkStream = tcpClient.GetStream();

        DisplayName = ((IPEndPoint)_tcpClient.Client.RemoteEndPoint).Address.ToString();
        CancellationToken.Register(() => _networkStream.Close());

        base.Start();
    }

    #endregion

    #region Properties

    public override string DisplayName { get; }

    protected override bool IsResponseRequired => true;

    #endregion

    #region Methods

    public override async Task ReceiveRequestAsync()
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

    public override void OnResponseReady(int frameLength)
    {
        _networkStream.Write(FrameBuffer.Buffer, 0, frameLength);
    }

    public virtual async Task<bool> TryReceiveRequestAsync()
    {
        // Whenever the network stream has a read timeout set, a TimeoutException
        // might occur which is catched later in ReceiveRequestAsync() where the token is
        // cancelled. Up to 1 second later, the connection clean up method detects that the 
        // token has been cancelled and removes the client from the list of connectected
        // clients.

        int partialLength;
        bool isParsed;

        isParsed = false;

        Length = 0;
        _bytesFollowing = 0;

        while (true)
        {
            if (_networkStream.DataAvailable)
            {
                partialLength = _networkStream.Read(FrameBuffer.Buffer, 0, FrameBuffer.Buffer.Length);
            }
            else
            {
                // actually, CancellationToken is ignored - therefore: CancellationToken.Register(() => ...);
                partialLength = await _networkStream.ReadAsync(FrameBuffer.Buffer, 0, FrameBuffer.Buffer.Length, CancellationToken);
            }

            if (partialLength > 0)
            {
                Length += partialLength;

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
            else
            {
                Length = 0;
                break;
            }
        }

        // Make sure that the incoming frame is actually addressed to this server.
        // If we have only one UnitIdentifier, and it is zero, then we accept all 
        // incoming messages
        if (ModbusServer.IsSingleZeroUnitMode || ModbusServer.UnitIdentifiers.Contains(UnitIdentifier))
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
                _tcpClient.Close();

            _disposedValue = true;
        }

        base.Dispose(disposing);
    }

    #endregion
}