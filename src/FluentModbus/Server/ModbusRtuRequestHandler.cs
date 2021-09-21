using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FluentModbus
{
    internal class ModbusRtuRequestHandler : ModbusRequestHandler, IDisposable
    {
        #region Fields

        private IModbusRtuSerialPort _serialPort;

        #endregion

        #region Constructors

        public ModbusRtuRequestHandler(IModbusRtuSerialPort serialPort, ModbusRtuServer rtuServer) : base(rtuServer, 256)
        {
            _serialPort = serialPort;
            _serialPort.Open();

            base.Start();
        }

        #endregion

        #region Properties

        public override string DisplayName => _serialPort.PortName;

        protected override bool IsResponseRequired => this.ModbusServer.UnitIdentifiers.Contains(this.UnitIdentifier);

        #endregion

        #region Methods

        internal override async Task ReceiveRequestAsync()
        {
            if (this.CTS.IsCancellationRequested)
                return;

            this.IsReady = false;

            try
            {
                await this.InternalReceiveRequestAsync();

                this.IsReady = true; // only when IsReady = true, this.WriteResponse() can be called

                if (this.ModbusServer.IsAsynchronous)
                    this.WriteResponse();
            }
            catch (Exception)
            {
                this.CTS.Cancel();
            }
        }

        protected override int WriteFrame(Action extendFrame)
        {
            int frameLength;
            ushort crc;

            this.FrameBuffer.Writer.Seek(0, SeekOrigin.Begin);

            // add unit identifier
            this.FrameBuffer.Writer.Write(this.UnitIdentifier);

            // add PDU
            extendFrame();

            // add CRC
            frameLength = unchecked((int)this.FrameBuffer.Writer.BaseStream.Position);
            crc = ModbusUtils.CalculateCRC(this.FrameBuffer.Buffer.AsMemory(0, frameLength));
            this.FrameBuffer.Writer.Write(crc);

            return frameLength + 2;
        }

        protected override void OnResponseReady(int frameLength)
        {
            _serialPort.Write(this.FrameBuffer.Buffer, 0, frameLength);
        }

        private async Task<bool> InternalReceiveRequestAsync()
        {
            this.Length = 0;

            try
            {
                while (true)
                {
                    this.Length += await _serialPort.ReadAsync(this.FrameBuffer.Buffer, this.Length, this.FrameBuffer.Buffer.Length - this.Length, this.CTS.Token);

                    // full frame received
                    if (ModbusUtils.DetectFrame(255, this.FrameBuffer.Buffer.AsMemory(0, this.Length)))
                    {
                        this.FrameBuffer.Reader.BaseStream.Seek(0, SeekOrigin.Begin);

                        // read unit identifier
                        this.UnitIdentifier = this.FrameBuffer.Reader.ReadByte();

                        break;
                    }
                    else
                    {
                        // reset length because one or more chunks of data were received and written to
                        // the buffer, but no valid Modbus frame could be detected and now the buffer is full
                        if (this.Length == this.FrameBuffer.Buffer.Length)
                            this.Length = 0;
                    }
                }
            }
            catch (TimeoutException)
            {
                this.Length = 0;
            }

            // make sure that the incoming frame is actually adressed to this server
            if (this.ModbusServer.UnitIdentifiers.Contains(this.UnitIdentifier))
            {
                this.LastRequest.Restart();
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    _serialPort.Close();

                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
