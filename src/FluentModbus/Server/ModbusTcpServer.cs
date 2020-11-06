using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
    /// <summary>
    /// A Modbus TCP server.
    /// </summary>
    public class ModbusTcpServer : ModbusServer
    {
        #region Fields

        private TcpListener _tcpListener;

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
            this.Logger = logger;
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
        public int ConnectionCount => this.RequestHandlers.Count;

        internal static TimeSpan DefaultConnectionTimeout { get; set; } = TimeSpan.FromMinutes(1);

        internal List<ModbusTcpRequestHandler> RequestHandlers { get; private set; }

        private ILogger Logger { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Starts the server. It will listen on any IP address on port 502.
        /// </summary>
        public new void Start()
        {
            this.Start(new IPEndPoint(IPAddress.Any, 502));
        }

        /// <summary>
        /// Starts the server. It will listen on the provided <paramref name="ipAddress"/> on port 502.
        /// </summary>
        public void Start(IPAddress ipAddress)
        {
            this.Start(new IPEndPoint(ipAddress, 502));
        }

        /// <summary>
        /// Starts the server. It will listen on the provided <paramref name="localEndpoint"/>.
        /// </summary>
        public void Start(IPEndPoint localEndpoint)
        {
            // "base..." is important!
            base.Stop();
            base.Start();

            this.RequestHandlers = new List<ModbusTcpRequestHandler>();

            _tcpListener = new TcpListener(localEndpoint);
            _tcpListener.Start();

            // accept clients asynchronously
            /* https://stackoverflow.com/questions/2782802/can-net-task-instances-go-out-of-scope-during-run */
            Task.Run(async () =>
            {
                while (!this.CTS.IsCancellationRequested)
                {
                    // There are no default timeouts (SendTimeout and ReceiveTimeout = 0), 
                    // use ConnectionTimeout instead.
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    var requestHandler = new ModbusTcpRequestHandler(tcpClient, this);

                    lock (this.Lock)
                    {
                        if (this.MaxConnections > 0 &&
                            /* request handler is added later in 'else' block, so count needs to be increased by 1 */
                            this.RequestHandlers.Count + 1 > this.MaxConnections)
                        {
                            tcpClient.Close();
                        }
                        else
                        {
                            this.RequestHandlers.Add(requestHandler);
                            this.Logger.LogInformation($"{this.RequestHandlers.Count} {(this.RequestHandlers.Count == 1 ? "client is" : "clients are")} connected");
                        }
                    }
                }
            }, this.CTS.Token);

            // remove clients asynchronously
            /* https://stackoverflow.com/questions/2782802/can-net-task-instances-go-out-of-scope-during-run */
            Task.Run(async () =>
            {
                while (!this.CTS.IsCancellationRequested)
                {
                    lock (this.Lock)
                    {
                        // see remarks to "TcpClient.Connected" property
                        // https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient.connected?redirectedfrom=MSDN&view=netframework-4.8#System_Net_Sockets_TcpClient_Connected
                        foreach (var requestHandler in this.RequestHandlers.ToList())
                        {
                            if (requestHandler.LastRequest.Elapsed > this.ConnectionTimeout)
                            {
                                this.Logger.LogInformation($"Connection {requestHandler.DisplayName} timed out.");

                                lock (this.Lock)
                                {
                                    // remove request handler
                                    this.RequestHandlers.Remove(requestHandler);
                                    this.Logger.LogInformation($"{this.RequestHandlers.Count} {(this.RequestHandlers.Count == 1 ? "client is" : "clients are")} connected");
                                }

                                requestHandler.Dispose();
                            }
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }, this.CTS.Token);
        }

        /// <summary>
        /// Starts the server. It will use only the provided <see cref="TcpClient"/>.
        /// </summary>
        /// <param name="tcpClient">The TCP client to communicate with.</param>
        public void Start(TcpClient tcpClient)
        {
            // "base..." is important!
            base.Stop();
            base.Start();

            this.RequestHandlers = new List<ModbusTcpRequestHandler>()
            {
                new ModbusTcpRequestHandler(tcpClient, this)
            };
        }

        /// <summary>
        /// Stops the server and closes all open TCP connections.
        /// </summary>
        public override void Stop()
        {
            base.Stop();

            _tcpListener?.Stop();

            this.RequestHandlers?.ForEach(requestHandler => requestHandler.Dispose());
        }

        ///<inheritdoc/>
        protected override void ProcessRequests()
        {
            lock (this.Lock)
            {
                foreach (var requestHandler in this.RequestHandlers)
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
}
