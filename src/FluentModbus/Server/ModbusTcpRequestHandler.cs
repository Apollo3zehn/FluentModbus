using System.Net;
using System.Net.Sockets;

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

        public ModbusTcpRequestHandler(TcpClient tcpClient, ModbusTcpServer tcpServer)
            : base(tcpServer, 260)
        {
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
            catch
            {
                CancelToken();
            }
        }

        protected override int WriteFrame(Action extendFrame)
        {
            int length;

            FrameBuffer.Writer.Seek(7, SeekOrigin.Begin);

            // add PDU
            extendFrame.Invoke();

            // add MBAP
            length = (int)FrameBuffer.Writer.BaseStream.Position;
            FrameBuffer.Writer.Seek(0, SeekOrigin.Begin);

            if (BitConverter.IsLittleEndian)
            {
                FrameBuffer.Writer.WriteReverse(_transactionIdentifier);
                FrameBuffer.Writer.WriteReverse(_protocolIdentifier);
                FrameBuffer.Writer.WriteReverse((byte)(length - 6));
            }
            else
            {
                FrameBuffer.Writer.Write(_transactionIdentifier);
                FrameBuffer.Writer.Write(_protocolIdentifier);
                FrameBuffer.Writer.Write((byte)(length - 6));
            }

            // add unit identifier
            // (the UnitIdentifier that originated in the Request, NOT the ActualUnitIdentifier)
            FrameBuffer.Writer.Write(UnitIdentifier);

            return length;
        }

        protected override void OnResponseReady(int frameLength)
        {
            _networkStream.Write(FrameBuffer.Buffer, 0, frameLength);
        }

        private async Task<bool> TryReceiveRequestAsync()
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

                    if (Length >= 7)
                    {
                        if (!isParsed) // read MBAP header only once
                        {
                            FrameBuffer.Reader.BaseStream.Seek(0, SeekOrigin.Begin);

                            // read MBAP header
                            _transactionIdentifier = FrameBuffer.Reader.ReadUInt16Reverse();   // 00-01  Transaction Identifier
                            _protocolIdentifier = FrameBuffer.Reader.ReadUInt16Reverse();      // 02-03  Protocol Identifier               
                            _bytesFollowing = FrameBuffer.Reader.ReadUInt16Reverse();          // 04-05  Length
                            UnitIdentifier = FrameBuffer.Reader.ReadByte();                    // 06     Unit Identifier

                            if (_protocolIdentifier != 0)
                            {
                                Length = 0;
                                break;
                            }

                            isParsed = true;
                        }

                        // full frame received
                        if (Length - 6 >= _bytesFollowing)
                        {
                            LastRequest.Restart();
                            break;
                        }
                    }
                }
                else
                {
                    Length = 0;
                    break;
                }
            }

            // make sure that the incoming frame is actually addressed to this server
            var actualUnitIdentifier = ModbusServer.GetActualUnitIdentifier(UnitIdentifier);
            if (actualUnitIdentifier.HasValue)
            {
                ActualUnitIdentifier = actualUnitIdentifier.Value;
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
}
