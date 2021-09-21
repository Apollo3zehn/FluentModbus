using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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

        private bool _handleUnitIdentifiers;

        #endregion

        #region Constructors

        public ModbusTcpRequestHandler(TcpClient tcpClient, ModbusTcpServer tcpServer, bool handleUnitIdentifiers = false /* For testing only */)
            : base(tcpServer, 260)
        {
            _tcpClient = tcpClient;
            _networkStream = tcpClient.GetStream();

            this.DisplayName = ((IPEndPoint)_tcpClient.Client.RemoteEndPoint).Address.ToString();
            this.CTS.Token.Register(() => _networkStream.Close());

            _handleUnitIdentifiers = handleUnitIdentifiers;

            base.Start();
        }

        #endregion

        #region Properties

        public override string DisplayName { get; }

        protected override bool IsResponseRequired => true;

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
            int length;

            this.FrameBuffer.Writer.Seek(7, SeekOrigin.Begin);

            // add PDU
            extendFrame.Invoke();

            // add MBAP
            length = (int)this.FrameBuffer.Writer.BaseStream.Position;
            this.FrameBuffer.Writer.Seek(0, SeekOrigin.Begin);

            if (BitConverter.IsLittleEndian)
            {
                this.FrameBuffer.Writer.WriteReverse(_transactionIdentifier);
                this.FrameBuffer.Writer.WriteReverse(_protocolIdentifier);
                this.FrameBuffer.Writer.WriteReverse((byte)(length - 6));
            }
            else
            {
                this.FrameBuffer.Writer.Write(_transactionIdentifier);
                this.FrameBuffer.Writer.Write(_protocolIdentifier);
                this.FrameBuffer.Writer.Write((byte)(length - 6));
            }

            this.FrameBuffer.Writer.Write(this.UnitIdentifier);

            return length;
        }

        protected override void OnResponseReady(int frameLength)
        {
            _networkStream.Write(this.FrameBuffer.Buffer, 0, frameLength);
        }

        private async Task<bool> InternalReceiveRequestAsync()
        {
            int partialLength;
            bool isParsed;

            isParsed = false;

            this.Length = 0;
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
                            var unitIdentifier = this.FrameBuffer.Reader.ReadByte();                    // 06     Unit Identifier

                            if (_handleUnitIdentifiers)
                                this.UnitIdentifier = unitIdentifier;

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
                    _tcpClient.Close();

                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
