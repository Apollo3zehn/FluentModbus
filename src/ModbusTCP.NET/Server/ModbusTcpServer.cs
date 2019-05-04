using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
        private List<ModbusTcpRequestHandler> _requestHandlersToRemove;

        private Task _task_update;
        private ManualResetEvent _manualResetEvent;
        private CancellationTokenSource _cts;

        private int _inputRegisterSize;
        private int _holdingRegisterSize;
        private int _coilSize;
        private int _discreteInputSize;

        ILogger _logger;

        #endregion

        #region Constructors

        public ModbusTcpServer(ILogger logger)
        {
            _logger = logger;

            _manualResetEvent = new ManualResetEvent(false);

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
                return !_manualResetEvent.WaitOne(TimeSpan.Zero);
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
            _requestHandlersToRemove = new List<ModbusTcpRequestHandler>();

            _tcpListener?.Stop();
            _tcpListener = new TcpListener(localEndpoint);
            _tcpListener.Start();

            _cts = new CancellationTokenSource();

            this.GetInputRegisterBuffer().Clear();
            this.GetHoldingRegisterBuffer().Clear();
            this.GetCoilBuffer().Clear();
            this.GetDiscreteInputBuffer().Clear();

            _task_update = Task.Run(() =>
            {
                _manualResetEvent.WaitOne();

                while (!_cts.IsCancellationRequested)
                {
                    this.ProcessRequests();

                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }
                    else
                    {
                        _manualResetEvent.Reset();
                        _manualResetEvent.WaitOne();
                    }
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _manualResetEvent?.Set();
            _task_update?.Wait();

            _tcpListener?.Stop();

            _requestHandlerSet?.ForEach(requestHandler =>
            {
                requestHandler.Dispose();
            });
        }

        public void Update()
        {
            _manualResetEvent.Set();
        }

        private void ProcessRequests()
        {
            while (_tcpListener.Pending())
            {
                TcpClient tcpClient;

                tcpClient = _tcpListener.AcceptTcpClient();

                _requestHandlerSet.Add(new ModbusTcpRequestHandler(tcpClient, this));
                _logger.LogInformation($"{_requestHandlerSet.Count} {(_requestHandlerSet.Count == 1 ? "client is" : "clients are")} connected");
            }

            _requestHandlerSet.ForEach(requestHandler =>
            {
                if (requestHandler.IsReady)
                {
                    if (requestHandler.Length > 0)
                    {
                        requestHandler.WriteResponse();
                    }

                    requestHandler.ReceiveRequestAsync();
                }
                else if (!requestHandler.IsConnected || requestHandler.LastRequest.Elapsed > TimeSpan.FromSeconds(20))
                {
                    _requestHandlersToRemove.Add(requestHandler);
                }
            });

            _requestHandlersToRemove.ForEach(requestHandler =>
            {
                _requestHandlerSet.Remove(requestHandler);
                requestHandler.Dispose();
                _logger.LogInformation($"{_requestHandlerSet.Count} {(_requestHandlerSet.Count == 1 ? "client is" : "clients are")} connected");
            });

            _requestHandlersToRemove.Clear();
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
