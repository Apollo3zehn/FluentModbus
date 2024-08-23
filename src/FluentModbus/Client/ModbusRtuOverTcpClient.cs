using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace FluentModbus
{
    /// <summary>
    /// A Modbus TCP client.
    /// </summary>
    public partial class ModbusRtuOverTcpClient : ModbusClient, IDisposable
    {
        #region Fields

        private (TcpClient Value, bool IsInternal)? _tcpClient;
        private NetworkStream _networkStream = default!;
        private ModbusFrameBuffer _frameBuffer = default!;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new Modbus TCP client for communication with Modbus TCP servers or bridges, routers and gateways for communication with serial line end units.
        /// </summary>
        public ModbusRtuOverTcpClient()
        {
            //
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the connection status of the underlying TCP client.
        /// </summary>
        public override bool IsConnected => _tcpClient?.Value.Connected ?? false;
        
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
        /// Connect to localhost at port 502 with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout.
        /// </summary>
        public void Connect()
        {
            Connect(ModbusEndianness.LittleEndian);
        }

        /// <summary>
        /// Connect to localhost at port 502. 
        /// </summary>
        /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
        public void Connect(ModbusEndianness endianness)
        {
            Connect(new IPEndPoint(IPAddress.Loopback, 502), endianness);
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteEndpoint"/>.
        /// </summary>
        /// <param name="remoteEndpoint">The IP address and optional port of the end unit with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout. Examples: "192.168.0.1", "192.168.0.1:502", "::1", "[::1]:502". The default port is 502.</param>
        public void Connect(string remoteEndpoint)
        {
            Connect(remoteEndpoint, ModbusEndianness.LittleEndian);
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteEndpoint"/>.
        /// </summary>
        /// <param name="remoteEndpoint">The IP address and optional port of the end unit. Examples: "192.168.0.1", "192.168.0.1:502", "::1", "[::1]:502". The default port is 502.</param>
        /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
        public void Connect(string remoteEndpoint, ModbusEndianness endianness)
        {
            if (!ModbusUtils.TryParseEndpoint(remoteEndpoint.AsSpan(), out var parsedRemoteEndpoint))
                throw new FormatException("An invalid IPEndPoint was specified.");

        #if NETSTANDARD2_0
            Connect(parsedRemoteEndpoint!, endianness);
        #endif
        
        #if NETSTANDARD2_1_OR_GREATER
            Connect(parsedRemoteEndpoint, endianness);
        #endif
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteIpAddress"/> at port 502.
        /// </summary>
        /// <param name="remoteIpAddress">The IP address of the end unit with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout. Example: IPAddress.Parse("192.168.0.1").</param>
        public void Connect(IPAddress remoteIpAddress)
        {
            Connect(remoteIpAddress, ModbusEndianness.LittleEndian);
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteIpAddress"/> at port 502.
        /// </summary>
        /// <param name="remoteIpAddress">The IP address of the end unit. Example: IPAddress.Parse("192.168.0.1").</param>
        /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
        public void Connect(IPAddress remoteIpAddress, ModbusEndianness endianness)
        {
            Connect(new IPEndPoint(remoteIpAddress, 502), endianness);
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteEndpoint"/> with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout.
        /// </summary>
        /// <param name="remoteEndpoint">The IP address and port of the end unit.</param>
        public void Connect(IPEndPoint remoteEndpoint)
        {
            Connect(remoteEndpoint, ModbusEndianness.LittleEndian);
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteEndpoint"/>.
        /// </summary>
        /// <param name="remoteEndpoint">The IP address and port of the end unit.</param>
        /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
        public void Connect(IPEndPoint remoteEndpoint, ModbusEndianness endianness)
        {
            Initialize(new TcpClient(), remoteEndpoint, endianness);
        }

        /// <summary>
        /// Initialize the Modbus TCP client with an externally managed <see cref="TcpClient"/>.
        /// </summary>
        /// <param name="tcpClient">The externally managed <see cref="TcpClient"/>.</param>
        /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
        public void Initialize(TcpClient tcpClient, ModbusEndianness endianness)
        {
            Initialize(tcpClient, default, endianness);
        }

        private void Initialize(TcpClient tcpClient, IPEndPoint? remoteEndpoint, ModbusEndianness endianness)
        {
            base.SwapBytes = BitConverter.IsLittleEndian && endianness == ModbusEndianness.BigEndian || 
                            !BitConverter.IsLittleEndian && endianness == ModbusEndianness.LittleEndian;

            _frameBuffer = new ModbusFrameBuffer(size: 260);

            if (_tcpClient.HasValue && _tcpClient.Value.IsInternal)
                _tcpClient.Value.Value.Close();

            var isInternal = remoteEndpoint is not null;
            _tcpClient = (tcpClient, isInternal);

            if (remoteEndpoint is not null && !tcpClient.ConnectAsync(remoteEndpoint.Address, remoteEndpoint.Port).Wait(ConnectTimeout))
                throw new Exception(ErrorMessage.ModbusClient_TcpConnectTimeout);

            // Why no method signature with NetworkStream only and then set the timeouts 
            // in the Connect method like for the RTU client?
            //
            // "If a NetworkStream was associated with a TcpClient, the Close method will
            //  close the TCP connection, but not dispose of the associated TcpClient."
            // -> https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream.close?view=net-6.0

            _networkStream = tcpClient.GetStream();

            if (isInternal)
            {
                _networkStream.ReadTimeout = ReadTimeout;
                _networkStream.WriteTimeout = WriteTimeout;
            }
        }

        /// <summary>
        /// Disconnect from the end unit.
        /// </summary>
        public void Disconnect()
        {
            if (_tcpClient.HasValue && _tcpClient.Value.IsInternal)
                _tcpClient.Value.Value.Close();
                
            _frameBuffer?.Dispose();

            // workaround for https://github.com/Apollo3zehn/FluentModbus/issues/44#issuecomment-747321152
            if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
                _tcpClient = null;
        }

        ///<inheritdoc/>
        protected override Span<byte> TransceiveFrame(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame)
        {
            // WARNING: IF YOU EDIT THIS METHOD, REFLECT ALL CHANGES ALSO IN TransceiveFrameAsync!

            var frameBuffer = _frameBuffer;
            var writer = _frameBuffer.Writer;
            var reader = _frameBuffer.Reader;

            // build request
            if (!(0 <= unitIdentifier && unitIdentifier <= 247))
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidUnitIdentifier);

            // special case: broadcast (only for write commands)
            if (unitIdentifier == 0)
            {
                switch (functionCode)
                {
                    case ModbusFunctionCode.WriteMultipleRegisters:
                    case ModbusFunctionCode.WriteSingleCoil:
                    case ModbusFunctionCode.WriteSingleRegister:
                    case ModbusFunctionCode.WriteMultipleCoils:
                    case ModbusFunctionCode.WriteFileRecord:
                    case ModbusFunctionCode.MaskWriteRegister:
                        break;
                    default:
                        throw new ModbusException(ErrorMessage.Modbus_InvalidUseOfBroadcast);
                }
            }

            writer.Seek(0, SeekOrigin.Begin);
            writer.Write(unitIdentifier);                                      // 00     Unit Identifier
            extendFrame(writer);

            var frameLength = (int)writer.BaseStream.Position;

            // add CRC
            var crc = ModbusUtils.CalculateCRC(frameBuffer.Buffer.AsMemory()[..frameLength]);
            writer.Write(crc);
            frameLength = (int)writer.BaseStream.Position;

            // send request
            _networkStream.Write(frameBuffer.Buffer, 0, frameLength);

            // special case: broadcast (only for write commands)
            if (unitIdentifier == 0)
                return _frameBuffer.Buffer.AsSpan(0, 0);

            // wait for and process response
            frameLength = 0;
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                // ASYNC-ONLY: using var timeoutCts = new CancellationTokenSource(_networkStream.ReadTimeout);
                // ASYNC-ONLY: 
                // ASYNC-ONLY: // https://stackoverflow.com/a/62162138
                // ASYNC-ONLY: // https://github.com/Apollo3zehn/FluentModbus/blob/181586d88cbbef3b2b3e6ace7b29099e04b30627/src/FluentModbus/ModbusRtuSerialPort.cs#L54
                // ASYNC-ONLY: using (timeoutCts.Token.Register(_networkStream.Close))
                // ASYNC-ONLY: using (cancellationToken.Register(timeoutCts.Cancel))
                // ASYNC-ONLY: {
                // ASYNC-ONLY:     try
                // ASYNC-ONLY:     {
                        frameLength += _networkStream.Read(frameBuffer.Buffer, frameLength, frameBuffer.Buffer.Length - frameLength);
                // ASYNC-ONLY:     }
                // ASYNC-ONLY:     catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                // ASYNC-ONLY:     {
                // ASYNC-ONLY:         throw;
                // ASYNC-ONLY:     }
                // ASYNC-ONLY:     catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                // ASYNC-ONLY:     {
                // ASYNC-ONLY:         throw new TimeoutException("The asynchronous read operation timed out.");
                // ASYNC-ONLY:     }
                // ASYNC-ONLY:     catch (IOException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                // ASYNC-ONLY:     {
                // ASYNC-ONLY:         throw new TimeoutException("The asynchronous read operation timed out.");
                // ASYNC-ONLY:     }
                // ASYNC-ONLY: }

                /* From MSDN (https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.read):
                 * Implementations of this method read a maximum of count bytes from the current stream and store 
                 * them in buffer beginning at offset. The current position within the stream is advanced by the 
                 * number of bytes read; however, if an exception occurs, the current position within the stream 
                 * remains unchanged. Implementations return the number of bytes read. The implementation will block 
                 * until at least one byte of data can be read, in the event that no data is available. Read returns
                 * 0 only when there is no more data in the stream and no more is expected (such as a closed socket or end of file).
                 * An implementation is free to return fewer bytes than requested even if the end of the stream has not been reached.
                 */
                
                if (ModbusUtils.DetectResponseFrame(unitIdentifier, _frameBuffer.Buffer.AsMemory()[..frameLength]))
                {
                    break;
                }
                else
                {
                    // reset length because one or more chunks of data were received and written to
                    // the buffer, but no valid Modbus frame could be detected and now the buffer is full
                    if (frameLength == _frameBuffer.Buffer.Length)
                        frameLength = 0;
                }
            }

            _ = reader.ReadByte();
            var rawFunctionCode = reader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                ProcessError(functionCode, (ModbusExceptionCode)_frameBuffer.Buffer[2]);

            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

            return _frameBuffer.Buffer.AsSpan(1, frameLength - 3);
        }

        #endregion

        #region IDisposable

        private bool _disposedValue;

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Disconnect();
                }

                _disposedValue = true;
            }
        }

        /// <summary>
        /// Diposes the current instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
