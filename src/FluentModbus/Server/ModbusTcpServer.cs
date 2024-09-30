using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;

namespace FluentModbus;

/// <summary>
/// A Modbus TCP server.
/// </summary>
public class ModbusTcpServer : ModbusServer
{
    #region Fields

    private bool _leaveOpen;
    private ITcpClientProvider? _tcpClientProvider;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a Modbus TCP server with support for holding registers (read and write, 16 bit), input registers (read-only, 16 bit), coils (read and write, 1 bit) and discete inputs (read-only, 1 bit).
    /// </summary>
    public ModbusTcpServer() : this(NullLogger.Instance, true)
    {
        //
    }

    /// <summary>
    /// Creates a Modbus TCP server with support for holding registers (read and write, 16 bit), input registers (read-only, 16 bit), coils (read and write, 1 bit) and discete inputs (read-only, 1 bit).
    /// </summary>
    /// <param name="logger">A logger instance to provide runtime information.</param>
    public ModbusTcpServer(ILogger logger) : this(logger, true)
    {
        //
    }

    /// <summary>
    /// Creates a Modbus TCP server with support for holding registers (read and write, 16 bit), input registers (read-only, 16 bit), coils (read and write, 1 bit) and discete inputs (read-only, 1 bit).
    /// </summary>
    /// <param name="isAsynchronous">Enables or disables the asynchronous operation, where each client request is processed immediately using a locking mechanism. Use synchronuous operation to avoid locks in the hosting application. See the <see href="https://github.com/Apollo3zehn/FluentModbus">documentation</see> for more details.</param>
    public ModbusTcpServer(bool isAsynchronous) : this(NullLogger.Instance, isAsynchronous)
    {
        //
    }

    /// <summary>
    /// Creates a Modbus TCP server with support for holding registers (read and write, 16 bit), input registers (read-only, 16 bit), coils (read and write, 1 bit) and discete inputs (read-only, 1 bit).
    /// </summary>
    /// <param name="logger">A logger instance to provide runtime information.</param>
    /// <param name="isAsynchronous">Enables or disables the asynchronous operation, where each client request is processed immediately using a locking mechanism. Use synchronuous operation to avoid locks in the hosting application. See the <see href="https://github.com/Apollo3zehn/FluentModbus">documentation</see> for more details.</param>
    public ModbusTcpServer(ILogger logger, bool isAsynchronous) : base(isAsynchronous)
    {
        Logger = logger;
        AddUnit(0);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the timeout for each client connection. When the client does not send any request within the specified period of time, the connection will be reset. Default is 1 minute.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = ModbusTcpServer.DefaultConnectionTimeout;

    /// <summary>
    /// Gets or sets the maximum number of concurrent client connections. A value of zero means there is no limit.
    /// </summary>
    public int MaxConnections { get; set; } = 0;

    /// <summary>
    /// Gets the number of currently connected clients.
    /// </summary>
    public int ConnectionCount => RequestHandlers.Count;
    
    internal static TimeSpan DefaultConnectionTimeout { get; set; } = TimeSpan.FromMinutes(1);

    internal List<ModbusTcpRequestHandler> RequestHandlers { get; private set; } = new List<ModbusTcpRequestHandler>();

    private ILogger Logger { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Starts the server. It will listen on any IP address on port 502.
    /// </summary>
    public void Start()
    {
        Start(new IPEndPoint(IPAddress.Any, 502));
    }

    /// <summary>
    /// Starts the server. It will listen on the provided <paramref name="ipAddress"/> on port 502.
    /// </summary>
    public void Start(IPAddress ipAddress)
    {
        Start(new IPEndPoint(ipAddress, 502));
    }

    /// <summary>
    /// Starts the server. It will listen on the provided <paramref name="localEndpoint"/>.
    /// </summary>
    public void Start(IPEndPoint localEndpoint)
    {
        Start(new DefaultTcpClientProvider(localEndpoint));
    }

    /// <summary>
    /// Starts the server. It will accept all TCP clients provided by the provided <see cref="ITcpClientProvider"/>.
    /// </summary>
    /// <param name="tcpClientProvider">The TCP client provider.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave the TCP client provider open after the <see cref="ModbusTcpServer"/> object is stopped or disposed; otherwise, <see langword="false"/>.</param>
    public void Start(ITcpClientProvider tcpClientProvider, bool leaveOpen = false)
    {
        _tcpClientProvider = tcpClientProvider;
        _leaveOpen = leaveOpen;

        base.StopProcessing();
        base.StartProcessing();

        RequestHandlers = new List<ModbusTcpRequestHandler>();

        // accept clients asynchronously
        /* https://stackoverflow.com/questions/2782802/can-net-task-instances-go-out-of-scope-during-run */
        Task.Run(async () =>
        {
            while (!CTS.IsCancellationRequested)
            {
                // There are no default timeouts (SendTimeout and ReceiveTimeout = 0), 
                // use ConnectionTimeout instead.
                var tcpClient = await _tcpClientProvider.AcceptTcpClientAsync();
                var requestHandler = new ModbusTcpRequestHandler(tcpClient, this);

                lock (Lock)
                {
                    if (MaxConnections > 0 &&
                        /* request handler is added later in 'else' block, so count needs to be increased by 1 */
                        RequestHandlers.Count + 1 > MaxConnections)
                    {
                        tcpClient.Close();
                    }

                    else
                    {
                        RequestHandlers.Add(requestHandler);
                        Logger.LogInformation($"{RequestHandlers.Count} {(RequestHandlers.Count == 1 ? "client is" : "clients are")} connected");
                    }
                }
            }
        }, CTS.Token);

        // remove clients asynchronously
        /* https://stackoverflow.com/questions/2782802/can-net-task-instances-go-out-of-scope-during-run */
        Task.Run(async () =>
        {
            while (!CTS.IsCancellationRequested)
            {
                lock (Lock)
                {
                    // see remarks to "TcpClient.Connected" property
                    // https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient.connected?redirectedfrom=MSDN&view=netframework-4.8#System_Net_Sockets_TcpClient_Connected
                    foreach (var requestHandler in RequestHandlers.ToList())
                    {
                        if (// This condition may become true if an external TcpClientProvider is used 
                            // and the user set a custom read timeout on the provided TcpClient.
                            // This should be the only cause but since "ReceiveRequestAsync" is never
                            // awaited, the actual cause may be different.
                            requestHandler.CancellationToken.IsCancellationRequested ||
                            // or there was not request received within the specified timeout
                            requestHandler.LastRequest.Elapsed > ConnectionTimeout)
                        {
                            try
                            {
                                Logger.LogInformation($"Connection {requestHandler.DisplayName} timed out.");

                                // remove request handler
                                RequestHandlers.Remove(requestHandler);
                                Logger.LogInformation($"{RequestHandlers.Count} {(RequestHandlers.Count == 1 ? "client is" : "clients are")} connected");

                                requestHandler.Dispose();
                            }
                            catch
                            {
                                // ignore error
                            }
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }, CTS.Token);
    }

    /// <summary>
    /// Starts the server. It will use only the provided <see cref="TcpClient"/>.
    /// </summary>
    /// <param name="tcpClient">The TCP client to communicate with.</param>
    public void Start(TcpClient tcpClient)
    {
        base.StopProcessing();
        base.StartProcessing();

        RequestHandlers = new List<ModbusTcpRequestHandler>()
        {
            new ModbusTcpRequestHandler(tcpClient, this)
        };
    }

    /// <summary>
    /// Stops the server and closes all open TCP connections.
    /// </summary>
    public override void Stop()
    {
        base.StopProcessing();

        if (!_leaveOpen)
            _tcpClientProvider?.Dispose();

        RequestHandlers.ForEach(requestHandler => requestHandler.Dispose());
    }

    ///<inheritdoc/>
    protected override void ProcessRequests()
    {
        lock (Lock)
        {
            foreach (var requestHandler in RequestHandlers)
            {
                if (requestHandler.IsReady)
                {
                    if (requestHandler.Length > 0)
                        requestHandler.WriteResponse();

                    _ = requestHandler.ReceiveRequestAsync();
                }
            }
        }
    }

    #endregion
}
