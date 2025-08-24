
/* This is automatically translated code. */
 
using System.Net;
using System.Net.Sockets;

namespace FluentModbus;

public partial class ModbusTcpClient
{
    /// <summary>
    /// Connect to localhost at port 502 with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await ConnectAsync(ModbusEndianness.LittleEndian, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to localhost at port 502. 
    /// </summary>
    /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    public async Task ConnectAsync(ModbusEndianness endianness, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(new IPEndPoint(IPAddress.Loopback, 502), endianness, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the specified <paramref name="remoteEndpoint"/>.
    /// </summary>
    /// <param name="remoteEndpoint">The IP address and optional port of the end unit with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout. Examples: "192.168.0.1", "192.168.0.1:502", "::1", "[::1]:502". The default port is 502.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    public async Task ConnectAsync(string remoteEndpoint, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(remoteEndpoint, ModbusEndianness.LittleEndian, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the specified <paramref name="remoteEndpoint"/>.
    /// </summary>
    /// <param name="remoteEndpoint">The IP address and optional port of the end unit. Examples: "192.168.0.1", "192.168.0.1:502", "::1", "[::1]:502". The default port is 502.</param>
    /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    public async Task ConnectAsync(string remoteEndpoint, ModbusEndianness endianness, CancellationToken cancellationToken = default)
    {
        if (!ModbusUtils.TryParseEndpoint(remoteEndpoint.AsSpan(), out var parsedRemoteEndpoint))
            throw new FormatException("An invalid IPEndPoint was specified.");

    #if NETSTANDARD2_0
        await ConnectAsync(parsedRemoteEndpoint!, endianness, cancellationToken).ConfigureAwait(false);
    #endif
    
    #if NETSTANDARD2_1_OR_GREATER
        await ConnectAsync(parsedRemoteEndpoint, endianness, cancellationToken).ConfigureAwait(false);
    #endif
    }

    /// <summary>
    /// Connect to the specified <paramref name="remoteIpAddress"/> at port 502.
    /// </summary>
    /// <param name="remoteIpAddress">The IP address of the end unit with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout. Example: IPAddress.Parse("192.168.0.1").</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    public async Task ConnectAsync(IPAddress remoteIpAddress, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(remoteIpAddress, ModbusEndianness.LittleEndian, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the specified <paramref name="remoteIpAddress"/> at port 502.
    /// </summary>
    /// <param name="remoteIpAddress">The IP address of the end unit. Example: IPAddress.Parse("192.168.0.1").</param>
    /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    public async Task ConnectAsync(IPAddress remoteIpAddress, ModbusEndianness endianness, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(new IPEndPoint(remoteIpAddress, 502), endianness, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the specified <paramref name="remoteEndpoint"/> with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout.
    /// </summary>
    /// <param name="remoteEndpoint">The IP address and port of the end unit.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    public async Task ConnectAsync(IPEndPoint remoteEndpoint, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(remoteEndpoint, ModbusEndianness.LittleEndian, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the specified <paramref name="remoteEndpoint"/>.
    /// </summary>
    /// <param name="remoteEndpoint">The IP address and port of the end unit.</param>
    /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    public async Task ConnectAsync(IPEndPoint remoteEndpoint, ModbusEndianness endianness, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(new TcpClient(), remoteEndpoint, endianness, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Initialize the Modbus TCP client with an externally managed <see cref="TcpClient"/>.
    /// </summary>
    /// <param name="tcpClient">The externally managed <see cref="TcpClient"/>.</param>
    /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    public async Task InitializeAsync(TcpClient tcpClient, ModbusEndianness endianness, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(tcpClient, default, endianness, cancellationToken).ConfigureAwait(false);
    }

    ///<inheritdoc/>
    private async Task InitializeAsync(TcpClient tcpClient, IPEndPoint? remoteEndpoint, ModbusEndianness endianness, CancellationToken cancellationToken = default)
    {
        base.SwapBytes = BitConverter.IsLittleEndian && endianness == ModbusEndianness.BigEndian || 
                        !BitConverter.IsLittleEndian && endianness == ModbusEndianness.LittleEndian;

        _frameBuffer = new ModbusFrameBuffer(size: 260);

        if (_tcpClient.HasValue && _tcpClient.Value.IsInternal)
            _tcpClient.Value.Value.Close();

        var isInternal = remoteEndpoint is not null;
        _tcpClient = (tcpClient, isInternal);

        if (remoteEndpoint is not null)
        {
            using var timeoutCts = new CancellationTokenSource(ConnectTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            var connectTask = tcpClient.ConnectAsync(remoteEndpoint.Address, remoteEndpoint.Port);
            var cancellationTask = Task.Delay(-1, linkedCts.Token);
            
            var completedTask = await Task.WhenAny(connectTask, cancellationTask).ConfigureAwait(false);
            
            if (completedTask == cancellationTask)
            {
                tcpClient.Close(); // Cancel the connect attempt
                
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);
               
                throw new Exception(ErrorMessage.ModbusClient_TcpConnectTimeout);
            }
            
            await connectTask.ConfigureAwait(false); // Surface any connection exceptions
        }

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

    ///<inheritdoc/>
    protected override async Task<Memory<byte>> TransceiveFrameAsync(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame, CancellationToken cancellationToken = default)
    {
        // WARNING: IF YOU EDIT THIS METHOD, REFLECT ALL CHANGES ALSO IN TransceiveFrameAsync!

        ushort bytesFollowing = 0;

        var frameBuffer = _frameBuffer;
        var writer = _frameBuffer.Writer;
        var reader = _frameBuffer.Reader;

        // build request
        writer.Seek(7, SeekOrigin.Begin);
        extendFrame(writer);
        var frameLength = (int)writer.BaseStream.Position;

        writer.Seek(0, SeekOrigin.Begin);

        if (BitConverter.IsLittleEndian)
        {
            writer.WriteReverse(GetTransactionIdentifier());                // 00-01  Transaction Identifier
            writer.WriteReverse((ushort)0);                                 // 02-03  Protocol Identifier
            writer.WriteReverse((ushort)(frameLength - 6));                 // 04-05  Length
        }

        else
        {
            writer.Write(GetTransactionIdentifier());                       // 00-01  Transaction Identifier
            writer.Write((ushort)0);                                        // 02-03  Protocol Identifier
            writer.Write((ushort)(frameLength - 6));                        // 04-05  Length
        }
        
        writer.Write(unitIdentifier);                                       // 06     Unit Identifier

        // send request
        await _networkStream.WriteAsync(frameBuffer.Buffer, 0, frameLength, cancellationToken).ConfigureAwait(false);

        // wait for and process response
        frameLength = 0;
        var isParsed = false;
        reader.BaseStream.Seek(0, SeekOrigin.Begin);

        while (true)
        {
            int partialLength;

            using var timeoutCts = new CancellationTokenSource(_networkStream.ReadTimeout);
            
            // https://stackoverflow.com/a/62162138
            // https://github.com/Apollo3zehn/FluentModbus/blob/181586d88cbbef3b2b3e6ace7b29099e04b30627/src/FluentModbus/ModbusRtuSerialPort.cs#L54
            using (timeoutCts.Token.Register(_networkStream.Close))
            using (cancellationToken.Register(timeoutCts.Cancel))
            {
                try
                {
                        partialLength = await _networkStream.ReadAsync(frameBuffer.Buffer, frameLength, frameBuffer.Buffer.Length - frameLength, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    throw new TimeoutException("The asynchronous read operation timed out.");
                }
                catch (IOException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("The asynchronous read operation timed out.");
                }
            }

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
                    _ = reader.ReadUInt16Reverse();                                     // 00-01  Transaction Identifier
                    var protocolIdentifier = reader.ReadUInt16Reverse();                // 02-03  Protocol Identifier               
                    bytesFollowing = reader.ReadUInt16Reverse();                        // 04-05  Length
                    _ = reader.ReadByte();                                              // 06     Unit Identifier

                    if (protocolIdentifier != 0)
                        throw new ModbusException(ErrorMessage.ModbusClient_InvalidProtocolIdentifier);

                    isParsed = true;
                }

                // full frame received
                if (frameLength - 6 >= bytesFollowing)
                    break;
            }
        }

        var rawFunctionCode = reader.ReadByte();

        if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
            ProcessError(functionCode, (ModbusExceptionCode)frameBuffer.Buffer[8]);

        else if (rawFunctionCode != (byte)functionCode)
            throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

        return frameBuffer.Buffer.AsMemory(7, frameLength - 7);
    }
}