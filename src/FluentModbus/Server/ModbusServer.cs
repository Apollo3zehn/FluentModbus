using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("FluentModbus.Tests")]

namespace FluentModbus
{
    /// Provides data for the registers changed event.
    public struct RegistersChangedEventArgs
    {
        /// <summary>
        /// The unit identifier for the registers that have changed.
        /// </summary>
        public byte UnitIdentifier { get; init; }

        /// <summary>
        /// A list of registers that have changed.
        /// </summary>
        public int[] Registers { get; init; }
    }

    /// <summary>
    /// Provides data for the coils changed event.
    /// </summary>
    public struct CoilsChangedEventArgs
    {
        /// <summary>
        /// The unit identifier for the coils that have changed.
        /// </summary>
        public byte UnitIdentifier { get; init; }

        /// <summary>
        /// A list of coils that have changed.
        /// </summary>
        public int[] Coils { get; init; }
    }

    /// <summary>
    /// Base class for a Modbus server.
    /// </summary>
    public abstract class ModbusServer : IDisposable
    {
        #region Events

        /// <summary>
        /// Occurs after one or more registers changed.
        /// </summary>
        public event EventHandler<RegistersChangedEventArgs> RegistersChanged;

        /// <summary>
        /// Occurs after one or more coils changed.
        /// </summary>
        public event EventHandler<CoilsChangedEventArgs> CoilsChanged;

        #endregion

        #region Fields

        private Task _task_process_requests;
        private ManualResetEventSlim _manualResetEvent;

        private Dictionary<byte, byte[]> _inputRegisterBufferMap = new();
        private Dictionary<byte, byte[]> _holdingRegisterBufferMap = new();
        private Dictionary<byte, byte[]> _coilBufferMap = new();
        private Dictionary<byte, byte[]> _discreteInputBufferMap = new();

        private int _inputRegisterSize;
        private int _holdingRegisterSize;
        private int _coilSize;
        private int _discreteInputSize;

        private List<byte> _unitIdentifiers = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ModbusServer"/>.
        /// </summary>
        /// <param name="isAsynchronous">A boolean which indicates if the server responds to client requests asynchronously (immediately) or synchronously (regularly at fixed events).</param>
        protected ModbusServer(bool isAsynchronous)
        {
            this.Lock = this;
            this.IsAsynchronous = isAsynchronous;

            this.MaxInputRegisterAddress = UInt16.MaxValue;
            this.MaxHoldingRegisterAddress = UInt16.MaxValue;
            this.MaxCoilAddress = UInt16.MaxValue;
            this.MaxDiscreteInputAddress = UInt16.MaxValue;

            _inputRegisterSize = (this.MaxInputRegisterAddress + 1) * 2;
            _holdingRegisterSize = (this.MaxHoldingRegisterAddress + 1) * 2;
            _coilSize = (this.MaxCoilAddress + 1 + 7) / 8;
            _discreteInputSize = (this.MaxDiscreteInputAddress + 1 + 7) / 8;

            _manualResetEvent = new ManualResetEventSlim(false);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the list of unit identifiers.
        /// </summary>
        public IReadOnlyList<byte> UnitIdentifiers 
            => _unitIdentifiers.AsReadOnly();

        /// <summary>
        /// Gets the lock object. For synchronous operation only.
        /// </summary>
        public object Lock { get; }

        /// <summary>
        /// Gets the operation mode.
        /// </summary>
        public bool IsAsynchronous { get; }

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

        /// <summary>
        /// Gets or sets a method that validates each client request.
        /// </summary>
        public Func<ModbusFunctionCode, ushort, ushort, ModbusExceptionCode> RequestValidator { get; set; }

        /// <summary>
        /// Gets or sets whether the events should be raised when register or coil data changes. Default: false.
        /// </summary>
        public bool EnableRaisingEvents { get; set; }

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
        /// Gets the input register as <see cref="UInt16"/> array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the input registers to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<short> GetInputRegisters(byte unitIdentifier = 0)
        {
            return MemoryMarshal.Cast<byte, short>(this.GetInputRegisterBuffer(unitIdentifier));
        }

        /// <summary>
        /// Gets the input register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        /// <param name="unitIdentifier">The unit identifier of the input register buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<T> GetInputRegisterBuffer<T>(byte unitIdentifier = 0) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetInputRegisterBuffer(unitIdentifier));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the input register buffer as byte array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the input register buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetInputRegisterBuffer(byte unitIdentifier)
        {
            return this.Find(unitIdentifier, this._inputRegisterBufferMap);
        }

        /// <summary>
        /// Gets the holding register as <see cref="UInt16"/> array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the holding registers to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<short> GetHoldingRegisters(byte unitIdentifier = 0)
        {
            return MemoryMarshal.Cast<byte, short>(this.GetHoldingRegisterBuffer(unitIdentifier));
        }

        /// <summary>
        /// Gets the holding register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        /// <param name="unitIdentifier">The unit identifier of the holding register buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<T> GetHoldingRegisterBuffer<T>(byte unitIdentifier = 0) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetHoldingRegisterBuffer(unitIdentifier));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the holding register buffer as byte array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the holding register buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetHoldingRegisterBuffer(byte unitIdentifier)
        {
            return this.Find(unitIdentifier, this._holdingRegisterBufferMap);
        }

        /// <summary>
        /// Gets the coils as <see cref="byte"/> array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the coils to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetCoils(byte unitIdentifier = 0)
        {
            return this.GetCoilBuffer(unitIdentifier);
        }

        /// <summary>
        /// Gets the coil buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        /// <param name="unitIdentifier">The unit identifier of the coil buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<T> GetCoilBuffer<T>(byte unitIdentifier = 0) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetCoilBuffer(unitIdentifier));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the coil buffer as byte array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the coil buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetCoilBuffer(byte unitIdentifier)
        {
            return this.Find(unitIdentifier, this._coilBufferMap);
        }

        /// <summary>
        /// Gets the discrete inputs as <see cref="byte"/> array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the discrete inputs to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetDiscreteInputs(byte unitIdentifier = 0)
        {
            return this.GetDiscreteInputBuffer(unitIdentifier);
        }

        /// <summary>
        /// Gets the discrete input buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        /// <param name="unitIdentifier">The unit identifier of the discrete input buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<T> GetDiscreteInputBuffer<T>(byte unitIdentifier = 0) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetDiscreteInputBuffer(unitIdentifier));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the discrete input buffer as byte array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the discrete input buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetDiscreteInputBuffer(byte unitIdentifier)
        {
            return this.Find(unitIdentifier, this._discreteInputBufferMap);
        }

        /// <summary>
        /// Clears all buffer contents.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public void ClearBuffers(byte unitIdentifier)
        {
            this.GetInputRegisterBuffer(unitIdentifier).Clear();
            this.GetHoldingRegisterBuffer(unitIdentifier).Clear();
            this.GetCoilBuffer(unitIdentifier).Clear();
            this.GetDiscreteInputBuffer(unitIdentifier).Clear();
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

        /// <summary>
        /// Process incoming requests.
        /// </summary>
        protected abstract void ProcessRequests();

        private Span<byte> Find(byte unitIdentifier, Dictionary<byte, byte[]> map)
        {
            if (unitIdentifier == 0)
            {
                if (map.Count == 1)
                    return map.First().Value;

                else
                    throw new Exception(ErrorMessage.ModbusServer_ZeroUnitOverloadOnlyApplicableInSingleUnitMode);
            }

            else
            {
                if (!map.TryGetValue(unitIdentifier, out var buffer))
                    throw new Exception(ErrorMessage.ModbusServer_ZeroUnitOverloadOnlyApplicableInSingleUnitMode);

                return buffer;
            }
        }

        private protected void AddUnit(byte unitIdentifer)
        {
            if (!_unitIdentifiers.Contains(unitIdentifer))
            {
                _unitIdentifiers.Add(unitIdentifer);
                _inputRegisterBufferMap[unitIdentifer] = new byte[_inputRegisterSize];
                _holdingRegisterBufferMap[unitIdentifer] = new byte[_holdingRegisterSize];
                _coilBufferMap[unitIdentifer] = new byte[_coilSize];
                _discreteInputBufferMap[unitIdentifer] = new byte[_discreteInputSize];
            }
        }

        private protected void RemoveUnit(byte unitIdentifer)
        {
            if (_unitIdentifiers.Contains(unitIdentifer))
            {
                _inputRegisterBufferMap.Remove(unitIdentifer);
                _holdingRegisterBufferMap.Remove(unitIdentifer);
                _coilBufferMap.Remove(unitIdentifer);
                _discreteInputBufferMap.Remove(unitIdentifer);
                _unitIdentifiers.Remove(unitIdentifer);
            }
        }

        internal void OnRegistersChanged(byte unitIdentifier, int[] registers)
        {
            this.RegistersChanged?.Invoke(this, new RegistersChangedEventArgs() 
            { 
                UnitIdentifier = unitIdentifier,
                Registers = registers 
            });
        }

        internal void OnCoilsChanged(byte unitIdentifier, int[] coils)
        {
            this.CoilsChanged?.Invoke(this, new CoilsChangedEventArgs()
            {
                UnitIdentifier = unitIdentifier,
                Coils = coils
            });
        }

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
