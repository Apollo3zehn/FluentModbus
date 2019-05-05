using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusTCP.NET
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

        public ModbusTcpServer(ILogger logger, bool isAsynchronous = true)
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

        public object Lock { get; }
        public bool IsAsynchronous { get; }

        public IntPtr InputRegisterBufferPtr { get; private set; }
        public IntPtr HoldingRegisterBufferPtr { get; private set; }
        public IntPtr CoilBufferPtr { get; private set; }
        public IntPtr DiscreteInputBufferPtr { get; private set; }

        public UInt16 MaxInputRegisterAddress { get; set; }
        public UInt16 MaxHoldingRegisterAddress { get; set; }
        public UInt16 MaxCoilAddress { get; set; }
        public UInt16 MaxDiscreteInputAddress { get; set; }

        public bool IsReady
        {
            get
            {
                return !_manualResetEvent.Wait(TimeSpan.Zero);
            }
        }

        #endregion

        #region Methods

        public unsafe Span<byte> GetInputRegisterBuffer()
        {
            return new Span<byte>(this.InputRegisterBufferPtr.ToPointer(), _inputRegisterSize);
        }

        public unsafe Span<byte> GetHoldingRegisterBuffer()
        {
            return new Span<byte>(this.HoldingRegisterBufferPtr.ToPointer(), _holdingRegisterSize);
        }

        public unsafe Span<byte> GetCoilBuffer()
        {
            return new Span<byte>(this.CoilBufferPtr.ToPointer(), _coilSize);
        }

        public unsafe Span<byte> GetDiscreteInputBuffer()
        {
            return new Span<byte>(this.DiscreteInputBufferPtr.ToPointer(), _discreteInputSize);
        }

        public void Start()
        {
            this.Start(new IPEndPoint(IPAddress.Any, 502));
        }

        public void Start(IPAddress ipAddress)
        {
            this.Start(new IPEndPoint(ipAddress, 502));
        }

        public void Start(IPEndPoint localEndpoint)
        {
            _requestHandlerSet = new List<ModbusTcpRequestHandler>();

            _tcpListener?.Stop();
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

        public void Update()
        {
            if (this.IsAsynchronous)
            {
                return;
            }

            _manualResetEvent.Set();
        }

        internal void AddRequestHandler(ModbusTcpRequestHandler handler)
        {
            lock (this.Lock)
            {
                _requestHandlerSet.Add(handler);
                _logger.LogInformation($"{_requestHandlerSet.Count} {(_requestHandlerSet.Count == 1 ? "client is" : "clients are")} connected");
            }
        }

        internal void RemoveRequestHandler(ModbusTcpRequestHandler handler)
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
