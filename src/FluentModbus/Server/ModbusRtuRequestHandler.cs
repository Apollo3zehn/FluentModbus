using System;
using System.IO;
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
        }

        #endregion

        #region Methods

        protected override async Task<bool> InternalReceiveRequestAsync()
        {
            try
            {
                while (true)
                {
                    this.Length += await _serialPort.ReadAsync(this.FrameBuffer.Buffer, this.Length, this.FrameBuffer.Buffer.Length - this.Length, this.CTS.Token);

                    // full frame received
                    if (ModbusUtils.DetectFrame(255, this.FrameBuffer.Buffer.AsSpan().Slice(0, this.Length)))
                    {
                        this.FrameBuffer.Reader.BaseStream.Seek(0, SeekOrigin.Begin);

                        // read unit identifier
                        this.UnitIdentifier = this.FrameBuffer.Reader.ReadByte();

                        break;
                    }
                }
            }
            catch (TimeoutException)
            {
                this.Length = 0;
            }

            // make sure that the incoming frame is actually adressed to this server
            if (this.UnitIdentifier == this.ModbusServer.UnitIdentifier)
            {
                this.LastRequest.Restart();
                return true;
            }
            else
            {
                return false;
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
            crc = ModbusUtils.CalculateCRC(this.FrameBuffer.Buffer.AsSpan().Slice(0, frameLength));
            this.FrameBuffer.Writer.Write(crc);

            return frameLength + 2;
        }

        protected override void OnResponseReady(int frameLength)
        {
            _serialPort.Write(this.FrameBuffer.Buffer, 0, frameLength);
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
