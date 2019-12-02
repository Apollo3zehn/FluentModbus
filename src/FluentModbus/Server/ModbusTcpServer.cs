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

        private Task _task_accept_clients;
        private Task _task_remove_clients;
        private Task _task_process_requests;

        private ManualResetEventSlim _manualResetEvent;
        private CancellationTokenSource _cts;

        ILogger _logger;

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
            _logger = logger;

            _manualResetEvent = new ManualResetEventSlim(false);

            this.ConnectionTimeout = TimeSpan.FromMinutes(1);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the timeout for each client connection. Wenn the client does not send any request within the specified time span, the connection will be reset. Default timeout is 1 minute.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; }

        internal List<ModbusTcpRequestHandler> RequestHandlerSet { get; private set; }

        private bool IsReady
        {
            get
            {
                return !_manualResetEvent.Wait(TimeSpan.Zero);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Starts the server. It will listen on any IP address on port 502.
        /// </summary>
        public void Start()
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
            this.Stop();

            this.RequestHandlerSet = new List<ModbusTcpRequestHandler>();

            _tcpListener = new TcpListener(localEndpoint);
            _tcpListener.Start();

            _cts = new CancellationTokenSource();

            this.ClearBuffers();

            // accept clients asynchronously
            _task_accept_clients = Task.Run(async () =>
            {
                ModbusTcpRequestHandler handler;
                TcpClient tcpClient;

                while (!_cts.IsCancellationRequested)
                {
                    tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    handler = new ModbusTcpRequestHandler(tcpClient, this);

                    this.AddRequestHandler(handler);
                }
            }, _cts.Token);

            // remove clients asynchronously
            _task_remove_clients = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    lock (this.Lock)
                    {
                        // see remarks to "TcpClient.Connected" property
                        // https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient.connected?redirectedfrom=MSDN&view=netframework-4.8#System_Net_Sockets_TcpClient_Connected
                        foreach (var requestHandler in this.RequestHandlerSet.ToList())
                        {
                            if (requestHandler.LastRequest.Elapsed > this.ConnectionTimeout)
                            {
                                this.RemoveRequestHandler(requestHandler);
                                requestHandler.Dispose();
                            }
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }, _cts.Token);

            if (!this.IsAsynchronous)
            {
                // only process requests when it is explicitly triggered
                _task_process_requests = Task.Run(() =>
                {
                    _manualResetEvent.Wait(_cts.Token);

                    while (!_cts.IsCancellationRequested)
                    {
                        this.ProcessRequests();

                        _manualResetEvent.Reset();
                        _manualResetEvent.Wait(_cts.Token);
                    }
                }, _cts.Token);
            }
        }

        /// <summary>
        /// Stops the server and closes all open client connections.
        /// </summary>
        public override void Stop()
        {
            _cts?.Cancel();

            _task_accept_clients = null;
            _task_remove_clients = null;

            _manualResetEvent?.Set();

            try
            {
                _task_process_requests?.Wait();
            }
            catch (Exception ex) when (ex.InnerException.GetType() == typeof(TaskCanceledException))
            {
                //
            }

            _tcpListener?.Stop();

            this.RequestHandlerSet?.ForEach(requestHandler =>
            {
                requestHandler.Dispose();
            });
        }

        /// <summary>
        /// Serve all available client requests. For synchronous operation only.
        /// </summary>
        public void Update()
        {
            if (this.IsAsynchronous || !this.IsReady)
                return;

            _manualResetEvent.Set();
        }

        private void AddRequestHandler(ModbusTcpRequestHandler handler)
        {
            lock (this.Lock)
            {
                this.RequestHandlerSet.Add(handler);
                _logger.LogInformation($"{this.RequestHandlerSet.Count} {(this.RequestHandlerSet.Count == 1 ? "client is" : "clients are")} connected");
            }
        }

        private void RemoveRequestHandler(ModbusTcpRequestHandler handler)
        {
            lock (this.Lock)
            {
                this.RequestHandlerSet.Remove(handler);
                _logger.LogInformation($"{this.RequestHandlerSet.Count} {(this.RequestHandlerSet.Count == 1 ? "client is" : "clients are")} connected");
            }
        }

        private void ProcessRequests()
        {
            lock (this.Lock)
            {
                foreach (var requestHandler in this.RequestHandlerSet)
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
