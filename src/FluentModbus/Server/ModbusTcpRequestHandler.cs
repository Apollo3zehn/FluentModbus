using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace FluentModbus
{
    internal class ModbusTcpRequestHandler : ModbusRequestHandler, IDisposable
    {
        #region Fields

        private TcpClient _tcpClient;
        private NetworkStream _networkStream;

        private ushort _transactionIdentifier;
        private ushort _protocolIdentifier;
        private ushort _bytesFollowing;

        #endregion

        #region Constructors

        public ModbusTcpRequestHandler(TcpClient tcpClient, ModbusTcpServer tcpServer) : base(tcpServer, 260)
        {
            _tcpClient = tcpClient;
            _networkStream = tcpClient.GetStream();

            this.CTS.Token.Register(() => _networkStream.Close());
        }

        #endregion

        #region Methods

        protected override async Task<bool> InternalReceiveRequestAsync()
        {
            int partialLength;
            bool isParsed;

            isParsed = false;

            _bytesFollowing = 0;

            while (true)
            {
                if (_networkStream.DataAvailable)
                {
                    partialLength = _networkStream.Read(this.FrameBuffer.Buffer, 0, this.FrameBuffer.Buffer.Length);
                }
                else
                {
                    // actually, CancellationToken is ignored - therefore: _cts.Token.Register(() => ...);
                    partialLength = await _networkStream.ReadAsync(this.FrameBuffer.Buffer, 0, this.FrameBuffer.Buffer.Length, this.CTS.Token);
                }

                if (partialLength > 0)
                {
                    this.Length += partialLength;

                    if (this.Length >= 7)
                    {
                        if (!isParsed) // read MBAP header only once
                        {
                            this.FrameBuffer.Reader.BaseStream.Seek(0, SeekOrigin.Begin);

                            // read MBAP header
                            _transactionIdentifier = this.FrameBuffer.Reader.ReadUInt16Reverse();       // 00-01  Transaction Identifier
                            _protocolIdentifier = this.FrameBuffer.Reader.ReadUInt16Reverse();          // 02-03  Protocol Identifier               
                            _bytesFollowing = this.FrameBuffer.Reader.ReadUInt16Reverse();              // 04-05  Length
                            this.UnitIdentifier = this.FrameBuffer.Reader.ReadByte();                   // 06     Unit Identifier

                            if (_protocolIdentifier != 0)
                            {
                                this.Length = 0;
                                break;
                            }

                            isParsed = true;
                        }

                        // full frame received
                        if (this.Length - 6 >= _bytesFollowing)
                        {
                            this.LastRequest.Restart();
                            break;
                        }
                    }
                }
                else
                {
                    this.Length = 0;
                    break;
                }
            }

            return true; // accept all incoming Modbus frames, no matter which unit identifier is set
        }

        protected override int WriteFrame(Action extendFrame)
        {
            int length;

            this.FrameBuffer.Writer.Seek(7, SeekOrigin.Begin);

            // add PDU
            extendFrame.Invoke();

            // add MBAP
            length = (int)this.FrameBuffer.Writer.BaseStream.Position;
            this.FrameBuffer.Writer.Seek(0, SeekOrigin.Begin);
            this.FrameBuffer.Writer.WriteReverse(_transactionIdentifier);
            this.FrameBuffer.Writer.WriteReverse(_protocolIdentifier);
            this.FrameBuffer.Writer.WriteReverse((byte)(length - 6));
            this.FrameBuffer.Writer.Write(this.UnitIdentifier);

            return length;
        }

        protected override void OnResponseReady(int frameLength)
        {
            _networkStream.Write(this.FrameBuffer.Buffer, 0, frameLength);
        }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)                    
                    _tcpClient.Close();

                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
