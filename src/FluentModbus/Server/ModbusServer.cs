using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("FluentModbus.Tests")]

namespace FluentModbus
{
    /// Provides data for the registers changed event.
    public readonly struct RegistersChangedEventArgs
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
    public readonly struct CoilsChangedEventArgs
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
        public event EventHandler<RegistersChangedEventArgs>? RegistersChanged;

        /// <summary>
        /// Occurs after one or more coils changed.
        /// </summary>
        public event EventHandler<CoilsChangedEventArgs>? CoilsChanged;

        #endregion

        #region Fields

        private Task? _task_process_requests;
        private readonly ManualResetEventSlim _manualResetEvent;

        private readonly Dictionary<byte, byte[]> _inputRegisterBufferMap = new();
        private readonly Dictionary<byte, byte[]> _holdingRegisterBufferMap = new();
        private readonly Dictionary<byte, byte[]> _coilBufferMap = new();
        private readonly Dictionary<byte, byte[]> _discreteInputBufferMap = new();

        private readonly int _inputRegisterSize;
        private readonly int _holdingRegisterSize;
        private readonly int _coilSize;
        private readonly int _discreteInputSize;

        private readonly List<byte> _unitIdentifiers = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ModbusServer"/>.
        /// </summary>
        /// <param name="isAsynchronous">A boolean which indicates if the server responds to client requests asynchronously (immediately) or synchronously (regularly at fixed events).</param>
        protected ModbusServer(bool isAsynchronous)
        {
            Lock = this;
            IsAsynchronous = isAsynchronous;

            MaxInputRegisterAddress = ushort.MaxValue;
            MaxHoldingRegisterAddress = ushort.MaxValue;
            MaxCoilAddress = ushort.MaxValue;
            MaxDiscreteInputAddress = ushort.MaxValue;

            _inputRegisterSize = (MaxInputRegisterAddress + 1) * 2;
            _holdingRegisterSize = (MaxHoldingRegisterAddress + 1) * 2;
            _coilSize = (MaxCoilAddress + 1 + 7) / 8;
            _discreteInputSize = (MaxDiscreteInputAddress + 1 + 7) / 8;

            _manualResetEvent = new ManualResetEventSlim(false);

            UnitIdentifiers = _unitIdentifiers.AsReadOnly();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets list of identifiers of the currently active units.
        /// </summary>
        public IReadOnlyList<byte> UnitIdentifiers { get; }

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
        public ushort MaxInputRegisterAddress { get; }

        /// <summary>
        /// Gets the maximum holding register address.
        /// </summary>
        public ushort MaxHoldingRegisterAddress { get; }

        /// <summary>
        /// Gets the maximum coil address.
        /// </summary>
        public ushort MaxCoilAddress { get; }

        /// <summary>
        /// Gets the maximum discrete input address.
        /// </summary>
        public ushort MaxDiscreteInputAddress { get; }

        /// <summary>
        /// Gets or sets a method that validates each client request.
        /// </summary>
        public Func<byte, ModbusFunctionCode, ushort, ushort, ModbusExceptionCode>? RequestValidator { get; set; }

        /// <summary>
        /// Gets or sets whether the events should be raised when register or coil data changes. Default: false.
        /// </summary>
        public bool EnableRaisingEvents { get; set; }

        /// <summary>
        /// Trigger the RegisterChanged or CoilsChanged event even when value has not been updated. Default: false.
        /// </summary>
        public bool AlwaysRaiseChangedEvent { get; set; } = false;

        internal bool IsSingleZeroUnitMode => UnitIdentifiers.Count == 1 && UnitIdentifiers[0] == 0;

        private protected CancellationTokenSource CTS { get; private set; } = new CancellationTokenSource();

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
        /// Gets the input register as <see cref="ushort"/> array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the input registers to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<short> GetInputRegisters(byte unitIdentifier = 0)
        {
            return MemoryMarshal.Cast<byte, short>(GetInputRegisterBuffer(unitIdentifier));
        }

        /// <summary>
        /// Gets the input register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        /// <param name="unitIdentifier">The unit identifier of the input register buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<T> GetInputRegisterBuffer<T>(byte unitIdentifier = 0) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(GetInputRegisterBuffer(unitIdentifier));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the input register buffer as byte array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the input register buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetInputRegisterBuffer(byte unitIdentifier = 0)
        {
            return Find(unitIdentifier, _inputRegisterBufferMap);
        }

        /// <summary>
        /// Gets the holding register as <see cref="ushort"/> array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the holding registers to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<short> GetHoldingRegisters(byte unitIdentifier = 0)
        {
            return MemoryMarshal.Cast<byte, short>(GetHoldingRegisterBuffer(unitIdentifier));
        }

        /// <summary>
        /// Gets the holding register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        /// <param name="unitIdentifier">The unit identifier of the holding register buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<T> GetHoldingRegisterBuffer<T>(byte unitIdentifier = 0) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(GetHoldingRegisterBuffer(unitIdentifier));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the holding register buffer as byte array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the holding register buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetHoldingRegisterBuffer(byte unitIdentifier = 0)
        {
            return Find(unitIdentifier, _holdingRegisterBufferMap);
        }

        /// <summary>
        /// Gets the coils as <see cref="byte"/> array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the coils to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetCoils(byte unitIdentifier = 0)
        {
            return GetCoilBuffer(unitIdentifier);
        }

        /// <summary>
        /// Gets the coil buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        /// <param name="unitIdentifier">The unit identifier of the coil buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<T> GetCoilBuffer<T>(byte unitIdentifier = 0) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(GetCoilBuffer(unitIdentifier));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the coil buffer as byte array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the coil buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetCoilBuffer(byte unitIdentifier = 0)
        {
            return Find(unitIdentifier, _coilBufferMap);
        }

        /// <summary>
        /// Gets the discrete inputs as <see cref="byte"/> array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the discrete inputs to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetDiscreteInputs(byte unitIdentifier = 0)
        {
            return GetDiscreteInputBuffer(unitIdentifier);
        }

        /// <summary>
        /// Gets the discrete input buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        /// <param name="unitIdentifier">The unit identifier of the discrete input buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<T> GetDiscreteInputBuffer<T>(byte unitIdentifier = 0) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(GetDiscreteInputBuffer(unitIdentifier));
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the discrete input buffer as byte array.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier of the discrete input buffer to return. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public Span<byte> GetDiscreteInputBuffer(byte unitIdentifier = 0)
        {
            return Find(unitIdentifier, _discreteInputBufferMap);
        }

        /// <summary>
        /// Clears all buffer contents.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier. A value of 0 means that the default unit identifier is used (for single-unit mode only).</param>
        public void ClearBuffers(byte unitIdentifier = 0)
        {
            GetInputRegisterBuffer(unitIdentifier).Clear();
            GetHoldingRegisterBuffer(unitIdentifier).Clear();
            GetCoilBuffer(unitIdentifier).Clear();
            GetDiscreteInputBuffer(unitIdentifier).Clear();
        }

        /// <summary>
        /// Serve all available client requests. For synchronous operation only.
        /// </summary>
        public void Update()
        {
            if (IsAsynchronous || !IsReady)
                return;

            _manualResetEvent.Set();
        }

        /// <summary>
        /// Stops the server operation and cleans up all resources.
        /// </summary>
        public virtual void Stop()
        {
            StopProcessing();
        }

        /// <summary>
        /// Stops the server operation.
        /// </summary>
        protected virtual void StopProcessing()
        {
            CTS?.Cancel();
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
        protected virtual void StartProcessing()
        {
            CTS = new CancellationTokenSource();

            if (!IsAsynchronous)
            {
                // only process requests when it is explicitly triggered
                _task_process_requests = Task.Run(() =>
                {
                    _manualResetEvent.Wait(CTS.Token);

                    while (!CTS.IsCancellationRequested)
                    {
                        ProcessRequests();

                        _manualResetEvent.Reset();
                        _manualResetEvent.Wait(CTS.Token);
                    }
                }, CTS.Token);
            }
        }

        /// <summary>
        /// Process incoming requests.
        /// </summary>
        protected abstract void ProcessRequests();

        /// <summary>
        /// Dynamically adds a new unit to the server.
        /// </summary>
        /// <param name="unitIdentifer">The identifier of the unit to add.</param>
        public void AddUnit(byte unitIdentifer)
        {
            // there are some or more unit identifiers - check if the operation is allowed
            if (_unitIdentifiers.Any())
            {
                if (unitIdentifer == 0)
                {
                    // we are not in single zero unit mode
                    if (!_unitIdentifiers.Contains(0))
                        throw new ArgumentException("Zero unit identifier can only be added in single zero unit identifier mode.");
                }

                else
                {
                    // we are in single zero unit mode -> remove zero unit identifier to leave that mode
                    if (_unitIdentifiers.Contains(0))
                        RemoveUnit(0);
                }
            }

            if (!_unitIdentifiers.Contains(unitIdentifer))
            {
                _unitIdentifiers.Add(unitIdentifer);
                _inputRegisterBufferMap[unitIdentifer] = new byte[_inputRegisterSize];
                _holdingRegisterBufferMap[unitIdentifer] = new byte[_holdingRegisterSize];
                _coilBufferMap[unitIdentifer] = new byte[_coilSize];
                _discreteInputBufferMap[unitIdentifer] = new byte[_discreteInputSize];
            }
        }

        /// <summary>
        /// Dynamically removes an existing unit from the server.
        /// </summary>
        /// <param name="unitIdentifer">The identifier of the unit to remove.</param>
        public void RemoveUnit(byte unitIdentifer)
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

        private Span<byte> Find(byte unitIdentifier, Dictionary<byte, byte[]> map)
        {
            if (!map.TryGetValue(unitIdentifier, out var buffer))
                throw new KeyNotFoundException(ErrorMessage.ModbusServer_UnitIdentifierNotFound);

            return buffer;
        }

        internal void OnRegistersChanged(byte unitIdentifier, int[] registers)
        {
            RegistersChanged?.Invoke(this, new RegistersChangedEventArgs() 
            { 
                UnitIdentifier = unitIdentifier,
                Registers = registers 
            });
        }

        internal void OnCoilsChanged(byte unitIdentifier, int[] coils)
        {
            CoilsChanged?.Invoke(this, new CoilsChangedEventArgs()
            {
                UnitIdentifier = unitIdentifier,
                Coils = coils
            });
        }

        #endregion

        #region IDisposable Support

        private bool _disposedValue = false;

        /// <summary>
        /// Disposes the <see cref="ModbusServer"/> and frees all managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">A value indicating if the finalizer or the dispose method triggered the dispose process.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                    StopProcessing();

                _disposedValue = true;
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
