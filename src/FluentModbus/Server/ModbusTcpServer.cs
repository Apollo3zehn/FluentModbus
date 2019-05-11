using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
    public class ModbusTcpServer : IDisposable
    {
        #region Fields

        private TcpListener _tcpListener;
        private List<ModbusTcpRequestHandler> _requestHandlerSet;

        private Task _task_accept_clients;
        private Task _task_remove_clients;
        private Task _task_process_requests;

        private ManualResetEventSlim _manualResetEvent;
        private CancellationTokenSource _cts;

        private int _inputRegisterSize;
        private int _holdingRegisterSize;
        private int _coilSize;
        private int _discreteInputSize;

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
        public ModbusTcpServer(ILogger logger, bool isAsynchronous)
        {
            _logger = logger;

            _manualResetEvent = new ManualResetEventSlim(false);

            this.Lock = new object();
            this.IsAsynchronous = isAsynchronous;

            this.MaxInputRegisterAddress = UInt16.MaxValue;
            this.MaxHoldingRegisterAddress = UInt16.MaxValue;
            this.MaxCoilAddress = UInt16.MaxValue;
            this.MaxDiscreteInputAddress = UInt16.MaxValue;

            _inputRegisterSize = (this.MaxInputRegisterAddress + 1) * 2;
            _holdingRegisterSize = (this.MaxHoldingRegisterAddress + 1) * 2;
            _coilSize = (this.MaxCoilAddress + 1 + 7) / 8;
            _discreteInputSize = (this.MaxDiscreteInputAddress + 1 + 7) / 8;

            this.InputRegisterBufferPtr = Marshal.AllocHGlobal(_inputRegisterSize);
            this.HoldingRegisterBufferPtr = Marshal.AllocHGlobal(_holdingRegisterSize);
            this.CoilBufferPtr = Marshal.AllocHGlobal(_coilSize);
            this.DiscreteInputBufferPtr = Marshal.AllocHGlobal(_discreteInputSize);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the lock object. For synchronous operation only.
        /// </summary>
        public object Lock { get; }

        /// <summary>
        /// Gets the operation mode.
        /// </summary>
        public bool IsAsynchronous { get; }

        /// <summary>
        /// Gets the pointer to the input registers buffer.
        /// </summary>
        public IntPtr InputRegisterBufferPtr { get; private set; }

        /// <summary>
        /// Gets the pointer to the holding registers buffer.
        /// </summary>
        public IntPtr HoldingRegisterBufferPtr { get; private set; }

        /// <summary>
        /// Gets the pointer to the coils buffer.
        /// </summary>
        public IntPtr CoilBufferPtr { get; private set; }

        /// <summary>
        /// Gets the pointer to the discete inputs buffer.
        /// </summary>
        public IntPtr DiscreteInputBufferPtr { get; private set; }

        /// <summary>
        /// Gets the maximum input register address.
        /// </summary>
        public UInt16 MaxInputRegisterAddress { get; set; }

        /// <summary>
        /// Gets the maximum holding register address.
        /// </summary>
        public UInt16 MaxHoldingRegisterAddress { get; set; }

        /// <summary>
        /// Gets the maximum coil address.
        /// </summary>
        public UInt16 MaxCoilAddress { get; set; }

        /// <summary>
        /// Gets the maximum discrete input address.
        /// </summary>
        public UInt16 MaxDiscreteInputAddress { get; set; }

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
        /// Gets the input register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetInputRegisterBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetInputRegisterBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the input register buffer as byte array.
        /// </summary>
        public unsafe Span<byte> GetInputRegisterBuffer()
        {
            return new Span<byte>(this.InputRegisterBufferPtr.ToPointer(), _inputRegisterSize);
        }

        /// <summary>
        /// Gets the holding register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetHoldingRegisterBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetHoldingRegisterBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the holding register buffer as byte array.
        /// </summary>
        public unsafe Span<byte> GetHoldingRegisterBuffer()
        {
            return new Span<byte>(this.HoldingRegisterBufferPtr.ToPointer(), _holdingRegisterSize);
        }

        /// <summary>
        /// Gets the coil buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetCoilBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetCoilBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the coil buffer as byte array.
        /// </summary>
        public unsafe Span<byte> GetCoilBuffer()
        {
            return new Span<byte>(this.CoilBufferPtr.ToPointer(), _coilSize);
        }

        /// <summary>
        /// Gets the discrete input buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetDiscreteInputBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetDiscreteInputBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the discrete input buffer as byte array.
        /// </summary>
        public unsafe Span<byte> GetDiscreteInputBuffer()
        {
            return new Span<byte>(this.DiscreteInputBufferPtr.ToPointer(), _discreteInputSize);
        }

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

            _requestHandlerSet = new List<ModbusTcpRequestHandler>();

            _tcpListener = new TcpListener(localEndpoint);
            _tcpListener.Start();

            _cts = new CancellationTokenSource();

            this.GetInputRegisterBuffer().Clear();
            this.GetHoldingRegisterBuffer().Clear();
            this.GetCoilBuffer().Clear();
            this.GetDiscreteInputBuffer().Clear();

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
                        foreach (var requestHandler in _requestHandlerSet.ToList())
                        {
                            if (requestHandler.LastRequest.Elapsed > TimeSpan.FromMinutes(1))
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
        public void Stop()
        {
            _cts?.Cancel();

            _task_accept_clients = null;
            _task_remove_clients = null;

            _manualResetEvent?.Set();
            _task_process_requests?.Wait();

            _tcpListener?.Stop();

            _requestHandlerSet?.ForEach(requestHandler =>
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
            {
                return;
            }

            _manualResetEvent.Set();
        }

        private void AddRequestHandler(ModbusTcpRequestHandler handler)
        {
            lock (this.Lock)
            {
                _requestHandlerSet.Add(handler);
                _logger.LogInformation($"{_requestHandlerSet.Count} {(_requestHandlerSet.Count == 1 ? "client is" : "clients are")} connected");
            }
        }

        private void RemoveRequestHandler(ModbusTcpRequestHandler handler)
        {
            lock (this.Lock)
            {
                _requestHandlerSet.Remove(handler);
                _logger.LogInformation($"{_requestHandlerSet.Count} {(_requestHandlerSet.Count == 1 ? "client is" : "clients are")} connected");
            }
        }

        private void ProcessRequests()
        {
            lock (this.Lock)
            {
                foreach (var requestHandler in _requestHandlerSet)
                {
                    if (requestHandler.IsReady)
                    {
                        if (requestHandler.Length > 0)
                        {
                            requestHandler.WriteResponse();
                        }

                        _ = requestHandler.ReceiveRequestAsync();
                    }
                }
            }
        }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.Stop();
                }

                Marshal.FreeHGlobal(this.InputRegisterBufferPtr);
                Marshal.FreeHGlobal(this.HoldingRegisterBufferPtr);
                Marshal.FreeHGlobal(this.CoilBufferPtr);
                Marshal.FreeHGlobal(this.DiscreteInputBufferPtr);

                disposedValue = true;
            }
        }

        ~ModbusTcpServer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Stops the server and disposes the buffers.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
