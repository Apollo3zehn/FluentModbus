using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace FluentModbus
{
    public class ModbusTcpClient : ModbusClient
    {
        #region Fields

        private ushort _transactionIdentifierBase;
        private object _transactionIdentifierLock;

        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private ModbusMessageBuffer _messageBuffer;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new Modbus TCP client for communication with Modbus TCP servers or bridges, routers and gateways for communication with serial line end units.
        /// </summary>
        public ModbusTcpClient()
        {
            _transactionIdentifierBase = 0;
            _transactionIdentifierLock = new object();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets or sets the connect timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int ConnectTimeout { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the read timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int ReadTimeout { get; set; } = 1000;

        /// <summary>
        /// Gets the connection status of the underlying TCP client.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return _tcpClient != null ? _tcpClient.Connected : false;
            }
        }

        /// <summary>
        /// Connect to localhost at port 502. 
        /// </summary>
        public void Connect()
        {
            this.Connect(new IPEndPoint(IPAddress.Loopback, 502));
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteIpAddress"/> at port 502.
        /// </summary>
        /// <param name="remoteIpAddress">The IP address of the end unit. Example: IPAddress.Parse("192.168.0.1").</param>
        public void Connect(IPAddress remoteIpAddress)
        {
            this.Connect(new IPEndPoint(remoteIpAddress, 502));
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteEndpoint"/>.
        /// </summary>
        /// <param name="remoteEndpoint">The IP address and port of the end unit.</param>
        public void Connect(IPEndPoint remoteEndpoint)
        {
            _messageBuffer = new ModbusMessageBuffer(260);

            _tcpClient?.Close();
            _tcpClient = new TcpClient();

            if (!_tcpClient.ConnectAsync(remoteEndpoint.Address, remoteEndpoint.Port).Wait(this.ConnectTimeout))
            {
                throw new Exception(ErrorMessage.ModbusClient_TcpConnectionTimeout);
            }

            _networkStream = _tcpClient.GetStream();
            _networkStream.ReadTimeout = this.ReadTimeout;
        }

        /// <summary>
        /// Disconnect from the end unit.
        /// </summary>
        public void Disconnect()
        {
            _tcpClient?.Close();
            _messageBuffer?.Dispose();
        }

        protected override Span<byte> TransceiveFrame(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame)
        {
            int totalLength;
            int newLength;

            ushort transactionIdentifier;
            ushort protocolIdentifier;
            ushort bytesFollowing;

            byte rawFunctionCode;

            ModbusMessageBuffer messageBuffer;
            ExtendedBinaryWriter requestWriter;
            ExtendedBinaryReader responseReader;

            bytesFollowing = 0;
            messageBuffer = _messageBuffer;
            requestWriter = _messageBuffer.RequestWriter;
            responseReader = _messageBuffer.ResponseReader;

            // build and send request
            requestWriter.Seek(7, SeekOrigin.Begin);
            extendFrame.Invoke(requestWriter);
            totalLength = (int)requestWriter.BaseStream.Position;

            requestWriter.Seek(0, SeekOrigin.Begin);
            requestWriter.WriteReverse(this.GetTransactionIdentifier());              // 00-01  Transaction Identifier
            requestWriter.WriteReverse((ushort)0);                                    // 02-03  Protocol Identifier
            requestWriter.WriteReverse((ushort)(totalLength - 6));                    // 04-05  Length
            requestWriter.Write(unitIdentifier);                                      // 06     Unit Identifier

            _networkStream.Write(messageBuffer.Buffer, 0, totalLength);

            // wait for and process response
            totalLength = 0;
            responseReader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                newLength = _networkStream.Read(messageBuffer.Buffer, totalLength, messageBuffer.Buffer.Length - totalLength);

                if (newLength == 0)
                    throw new InvalidOperationException(ErrorMessage.ModbusClient_TcpConnectionClosedUnexpectedly);

                totalLength += newLength;

                if (totalLength >= 7)
                {
                    if (responseReader.BaseStream.Position == 0) // read MBAP header only once
                    {
                        // read MBAP header
                        transactionIdentifier = responseReader.ReadUInt16Reverse();              // 00-01  Transaction Identifier
                        protocolIdentifier = responseReader.ReadUInt16Reverse();                 // 02-03  Protocol Identifier               
                        bytesFollowing = responseReader.ReadUInt16Reverse();                     // 04-05  Length
                        unitIdentifier = responseReader.ReadByte();                              // 06     Unit Identifier

                        if (protocolIdentifier != 0)
                        {
                            throw new ModbusException(ErrorMessage.ModbusClient_ProtocolIdentifierInvalid);
                        }
                    }

                    if (totalLength - 6 >= bytesFollowing)
                    {
                        break;
                    }
                }
            }

            rawFunctionCode = responseReader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                this.ProcessError(functionCode, (ModbusExceptionCode)messageBuffer.Buffer[8]);
            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_ResponseFunctionCodeInvalid);

            return messageBuffer.Buffer.AsSpan(7, totalLength - 7);
        }

        private ushort GetTransactionIdentifier()
        {
            lock (_transactionIdentifierLock)
            {
                return _transactionIdentifierBase++;
            }
        }

        #endregion
    }
}
