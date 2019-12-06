using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("FluentModbus.Tests")]

namespace FluentModbus
{
    /// <summary>
    /// Base class for a Modbus server.
    /// </summary>
    public abstract class ModbusServer : IDisposable
    {
        #region Fields

        private Task _task_process_requests;
        private ManualResetEventSlim _manualResetEvent;

        private int _inputRegisterSize;
        private int _holdingRegisterSize;
        private int _coilSize;
        private int _discreteInputSize;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ModbusServer"/>.
        /// </summary>
        /// <param name="isAsynchronous">A boolean which indicates if the server responds to client requests asynchronously (immediately) or synchronously (regularly at fixed events).</param>
        protected ModbusServer(bool isAsynchronous)
        {
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

            _manualResetEvent = new ManualResetEventSlim(false);
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
        public IntPtr InputRegisterBufferPtr { get; }

        /// <summary>
        /// Gets the pointer to the holding registers buffer.
        /// </summary>
        public IntPtr HoldingRegisterBufferPtr { get; }

        /// <summary>
        /// Gets the pointer to the coils buffer.
        /// </summary>
        public IntPtr CoilBufferPtr { get; }

        /// <summary>
        /// Gets the pointer to the discete inputs buffer.
        /// </summary>
        public IntPtr DiscreteInputBufferPtr { get; }

        /// <summary>
        /// Gets the maximum input register address.
        /// </summary>
        public UInt16 MaxInputRegisterAddress { get; }

        /// <summary>
        /// Gets the maximum holding register address.
        /// </summary>
        public UInt16 MaxHoldingRegisterAddress { get; }

        /// <summary>
        /// Gets the maximum coil address.
        /// </summary>
        public UInt16 MaxCoilAddress { get; }

        /// <summary>
        /// Gets the maximum discrete input address.
        /// </summary>
        public UInt16 MaxDiscreteInputAddress { get; }

        private protected CancellationTokenSource CTS { get; private set; }

        private protected bool IsReady
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
        /// Clears all buffer contents.
        /// </summary>
        public void ClearBuffers()
        {
            this.GetInputRegisterBuffer().Clear();
            this.GetHoldingRegisterBuffer().Clear();
            this.GetCoilBuffer().Clear();
            this.GetDiscreteInputBuffer().Clear();
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

        /// <summary>
        /// Stops the server operation.
        /// </summary>
        public virtual void Stop()
        {
            this.CTS?.Cancel();
            _manualResetEvent?.Set();

            try
            {
                _task_process_requests?.Wait();
            }
            catch (Exception ex) when (ex.InnerException.GetType() == typeof(TaskCanceledException))
            {
                //
            }

            this.ClearBuffers();
        }

        /// <summary>
        /// Starts the server operation.
        /// </summary>
        protected virtual void Start()
        {
            this.CTS = new CancellationTokenSource();

            if (!this.IsAsynchronous)
            {
                // only process requests when it is explicitly triggered
                _task_process_requests = Task.Run(() =>
                {
                    _manualResetEvent.Wait(this.CTS.Token);

                    while (!this.CTS.IsCancellationRequested)
                    {
                        this.ProcessRequests();

                        _manualResetEvent.Reset();
                        _manualResetEvent.Wait(this.CTS.Token);
                    }
                }, this.CTS.Token);
            }
        }

        private protected abstract void ProcessRequests();

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        /// <summary>
        /// Disposes the <see cref="ModbusServer"/> and frees all managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">A value indicating if the finalizer or the dispose method triggered the dispose process.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    this.Stop();

                Marshal.FreeHGlobal(this.InputRegisterBufferPtr);
                Marshal.FreeHGlobal(this.HoldingRegisterBufferPtr);
                Marshal.FreeHGlobal(this.CoilBufferPtr);
                Marshal.FreeHGlobal(this.DiscreteInputBufferPtr);

                disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes the <see cref="ModbusServer"/> and frees all managed and unmanaged resources.
        /// </summary>
        ~ModbusServer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes the buffers.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
