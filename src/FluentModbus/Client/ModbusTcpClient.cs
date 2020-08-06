using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FluentModbus
{
    /// <summary>
    /// A Modbus TCP client.
    /// </summary>
    public class ModbusTcpClient : ModbusClient
    {
        #region Fields

        private ushort _transactionIdentifierBase;
        private object _transactionIdentifierLock;

        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private ModbusFrameBuffer _frameBuffer;

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

        #region Properties

        /// <summary>
        /// Gets or sets the connect timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int ConnectTimeout { get; set; } = ModbusTcpClient.DefaultConnectTimeout;

        /// <summary>
        /// Gets or sets the read timeout in milliseconds. Default is <see cref="Timeout.Infinite"/>.
        /// </summary>
        public int ReadTimeout { get; set; } = Timeout.Infinite;

        /// <summary>
        /// Gets or sets the write timeout in milliseconds. Default is <see cref="Timeout.Infinite"/>.
        /// </summary>
        public int WriteTimeout { get; set; } = Timeout.Infinite;

        internal static int DefaultConnectTimeout { get; set; } = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;

        #endregion

        #region Methods

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
        /// Connect to localhost at port 502 with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout.
        /// </summary>
        public void Connect()
        {
            this.Connect(ModbusEndianness.LittleEndian);
        }

        /// <summary>
        /// Connect to localhost at port 502. 
        /// </summary>
        /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
        public void Connect(ModbusEndianness endianness)
        {
            this.Connect(new IPEndPoint(IPAddress.Loopback, 502), endianness);
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteIpAddress"/> at port 502.
        /// </summary>
        /// <param name="remoteIpAddress">The IP address of the end unit with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout. Example: IPAddress.Parse("192.168.0.1").</param>
        public void Connect(IPAddress remoteIpAddress)
        {
            this.Connect(remoteIpAddress, ModbusEndianness.LittleEndian);
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteIpAddress"/> at port 502.
        /// </summary>
        /// <param name="remoteIpAddress">The IP address of the end unit. Example: IPAddress.Parse("192.168.0.1").</param>
        /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
        public void Connect(IPAddress remoteIpAddress, ModbusEndianness endianness)
        {
            this.Connect(new IPEndPoint(remoteIpAddress, 502), endianness);
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteEndpoint"/> with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout.
        /// </summary>
        /// <param name="remoteEndpoint">The IP address and port of the end unit.</param>
        public void Connect(IPEndPoint remoteEndpoint)
        {
            this.Connect(remoteEndpoint, ModbusEndianness.LittleEndian);
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteEndpoint"/>.
        /// </summary>
        /// <param name="remoteEndpoint">The IP address and port of the end unit.</param>
        /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
        public void Connect(IPEndPoint remoteEndpoint, ModbusEndianness endianness)
        {
            base.SwapBytes = BitConverter.IsLittleEndian && endianness == ModbusEndianness.BigEndian
                         || !BitConverter.IsLittleEndian && endianness == ModbusEndianness.LittleEndian;

            _frameBuffer = new ModbusFrameBuffer(260);

            _tcpClient?.Close();
            _tcpClient = new TcpClient();

            if (!_tcpClient.ConnectAsync(remoteEndpoint.Address, remoteEndpoint.Port).Wait(this.ConnectTimeout))
                throw new Exception(ErrorMessage.ModbusClient_TcpConnectTimeout);

            _networkStream = _tcpClient.GetStream();
            _networkStream.ReadTimeout = this.ReadTimeout;
            _networkStream.WriteTimeout = this.WriteTimeout;
        }

        /// <summary>
        /// Disconnect from the end unit.
        /// </summary>
        public void Disconnect()
        {
            _tcpClient?.Close();
            _frameBuffer?.Dispose();
        }

        private protected override Span<byte> TransceiveFrame(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame)
        {
            int frameLength;
            int partialLength;

            ushort transactionIdentifier;
            ushort protocolIdentifier;
            ushort bytesFollowing;

            byte rawFunctionCode;

            bool isParsed;

            ModbusFrameBuffer frameBuffer;
            ExtendedBinaryWriter writer;
            ExtendedBinaryReader reader;

            bytesFollowing = 0;
            frameBuffer = _frameBuffer;
            writer = _frameBuffer.Writer;
            reader = _frameBuffer.Reader;

            // build request
            writer.Seek(7, SeekOrigin.Begin);
            extendFrame(writer);
            frameLength = (int)writer.BaseStream.Position;

            writer.Seek(0, SeekOrigin.Begin);

            if (BitConverter.IsLittleEndian)
            {
                writer.WriteReverse(this.GetTransactionIdentifier());          // 00-01  Transaction Identifier
                writer.WriteReverse((ushort)0);                                // 02-03  Protocol Identifier
                writer.WriteReverse((ushort)(frameLength - 6));                // 04-05  Length
            }
            else
            {
                writer.Write(this.GetTransactionIdentifier());                 // 00-01  Transaction Identifier
                writer.Write((ushort)0);                                       // 02-03  Protocol Identifier
                writer.Write((ushort)(frameLength - 6));                       // 04-05  Length
            }
            
            writer.Write(unitIdentifier);                                      // 06     Unit Identifier

            // send request
            _networkStream.Write(frameBuffer.Buffer, 0, frameLength);

            // wait for and process response
            frameLength = 0;
            isParsed = false;
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                partialLength = _networkStream.Read(frameBuffer.Buffer, frameLength, frameBuffer.Buffer.Length - frameLength);

                /* From MSDN (https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.read):
                 * Implementations of this method read a maximum of count bytes from the current stream and store 
                 * them in buffer beginning at offset. The current position within the stream is advanced by the 
                 * number of bytes read; however, if an exception occurs, the current position within the stream 
                 * remains unchanged. Implementations return the number of bytes read. The implementation will block 
                 * until at least one byte of data can be read, in the event that no data is available. Read returns
                 * 0 only when there is no more data in the stream and no more is expected (such as a closed socket or end of file).
                 * An implementation is free to return fewer bytes than requested even if the end of the stream has not been reached.
                 */
                if (partialLength == 0)
                    throw new InvalidOperationException(ErrorMessage.ModbusClient_TcpConnectionClosedUnexpectedly);

                frameLength += partialLength;

                if (frameLength >= 7)
                {
                    if (!isParsed) // read MBAP header only once
                    {
                        // read MBAP header
                        transactionIdentifier = reader.ReadUInt16Reverse();              // 00-01  Transaction Identifier
                        protocolIdentifier = reader.ReadUInt16Reverse();                 // 02-03  Protocol Identifier               
                        bytesFollowing = reader.ReadUInt16Reverse();                     // 04-05  Length
                        unitIdentifier = reader.ReadByte();                              // 06     Unit Identifier

                        if (protocolIdentifier != 0)
                            throw new ModbusException(ErrorMessage.ModbusClient_InvalidProtocolIdentifier);

                        isParsed = true;
                    }

                    // full frame received
                    if (frameLength - 6 >= bytesFollowing)
                        break;
                }
            }

            rawFunctionCode = reader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                this.ProcessError(functionCode, (ModbusExceptionCode)frameBuffer.Buffer[8]);
            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

            return frameBuffer.Buffer.AsSpan(7, frameLength - 7);
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
