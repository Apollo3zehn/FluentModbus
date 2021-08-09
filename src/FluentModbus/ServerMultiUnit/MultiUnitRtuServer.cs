using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus.ServerMultiUnit
{
    /// <summary>
    ///
    /// </summary>
    public class MultiUnitRtuServer
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="units"></param>
        /// <param name="isAsynchronous"></param>
        /// <param name="baudRate"></param>
        /// <param name="handshake"></param>
        /// <param name="parity"></param>
        /// <param name="stopBits"></param>
        /// <param name="readTimeout"></param>
        /// <param name="writeTimeout"></param>
        public MultiUnitRtuServer(byte[] units, bool isAsynchronous, int baudRate = 9600, Handshake handshake = Handshake.None, Parity parity = Parity.Even, StopBits stopBits = StopBits.One, int readTimeout = 1000, int writeTimeout = 1000)
        {
            this.units = units;

            this.Lock = this;
            this.IsAsynchronous = isAsynchronous;

            this.MaxInputRegisterAddress = ushort.MaxValue;
            this.MaxHoldingRegisterAddress = ushort.MaxValue;
            this.MaxCoilAddress = ushort.MaxValue;
            this.MaxDiscreteInputAddress = ushort.MaxValue;

            _inputRegisterSize = (this.MaxInputRegisterAddress + 1) * 2;
            _holdingRegisterSize = (this.MaxHoldingRegisterAddress + 1) * 2;
            _coilSize = (this.MaxCoilAddress + 1 + 7) / 8;
            _discreteInputSize = (this.MaxDiscreteInputAddress + 1 + 7) / 8;

            foreach (var unit in units)
            {
                _inputRegisterBuffer.Add(unit, new byte[_inputRegisterSize]);

                _holdingRegisterBuffer.Add(unit, new byte[_holdingRegisterSize]);

                _coilBuffer.Add(unit, new byte[_coilSize]);

                _discreteInputBuffer.Add(unit, new byte[_discreteInputSize]);
            }

            _manualResetEvent = new ManualResetEventSlim(false);
            BaudRate = baudRate;
            Handshake = handshake;
            Parity = parity;
            StopBits = stopBits;
            ReadTimeout = readTimeout;
            WriteTimeout = writeTimeout;
        }

        #region Fields

        private Task _task_process_requests;
        private ManualResetEventSlim _manualResetEvent;

        private Dictionary<byte, byte[]> _inputRegisterBuffer = new();
        private Dictionary<byte, byte[]> _holdingRegisterBuffer = new();
        private Dictionary<byte, byte[]> _coilBuffer = new();
        private Dictionary<byte, byte[]> _discreteInputBuffer = new();

        private int _inputRegisterSize;
        private int _holdingRegisterSize;
        private int _coilSize;
        private int _discreteInputSize;
        private readonly byte[] units;

        private IModbusRtuSerialPort _serialPort;

        #endregion Fields

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

        /// <summary>
        /// Gets or sets the serial baud rate. Default is 9600.
        /// </summary>
        public int BaudRate { get; }

        /// <summary>
        /// Gets or sets the handshaking protocol for serial port transmission of data. Default is Handshake.None.
        /// </summary>
        public Handshake Handshake { get; }

        /// <summary>
        /// Gets or sets the parity-checking protocol. Default is Parity.Even.
        /// </summary>
        public Parity Parity { get; }

        /// <summary>
        /// Gets or sets the standard number of stopbits per byte. Default is StopBits.One.
        /// </summary>
        public StopBits StopBits { get; }

        /// <summary>
        /// Gets or sets the read timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int ReadTimeout { get; }

        /// <summary>
        /// Gets or sets the write timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int WriteTimeout { get; }

        internal ModbusRtuRequestHandler RequestHandler { get; private set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Starts the server. It will listen on the provided <paramref name="port"/>.
        /// </summary>
        /// <param name="port">The COM port to be used, e.g. COM1.</param>
        public void Start(string port)
        {
            IModbusRtuSerialPort serialPort = new ModbusRtuSerialPort(new SerialPort(port)
            {
                BaudRate = this.BaudRate,
                Handshake = this.Handshake,
                Parity = this.Parity,
                StopBits = this.StopBits,
                ReadTimeout = this.ReadTimeout,
                WriteTimeout = this.WriteTimeout
            });

            _serialPort = serialPort;

            if (this.Parity == Parity.None && this.StopBits != StopBits.Two)
                throw new InvalidOperationException(ErrorMessage.Modbus_NoParityRequiresTwoStopBits);

            // "base..." is important!
            this.StopListening();
            this.StartListening();

            this.RequestHandler = new ModbusRtuRequestHandler(serialPort, this);
        }

        /// <summary>
        /// Starts the server operation.
        /// </summary>
        protected virtual void StartListening()
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

        ///<inheritdoc/>
        protected void ProcessRequests()
        {
            lock (this.Lock)
            {
                if (this.RequestHandler.IsReady)
                {
                    if (this.RequestHandler.Length > 0)
                        this.RequestHandler.WriteResponse();

                    _ = this.RequestHandler.ReceiveRequestAsync();
                }
            }
        }

        /// <summary>
        /// Stops the server operation.
        /// </summary>
        public void StopListening()
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

            this.RequestHandler?.Dispose();
        }

        /// <summary>
        /// Gets the input register as <see cref="UInt16"/> array.
        /// </summary>
        public Span<short> GetInputRegisters()
        {
            return MemoryMarshal.Cast<byte, short>(this.GetInputRegisterBuffer());
        }

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
        public Span<byte> GetInputRegisterBuffer()
        {
            return _inputRegisterBuffer;
        }

        /// <summary>
        /// Gets the holding register as <see cref="UInt16"/> array.
        /// </summary>
        public Span<short> GetHoldingRegisters()
        {
            return MemoryMarshal.Cast<byte, short>(this.GetHoldingRegisterBuffer());
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
        public Span<byte> GetHoldingRegisterBuffer()
        {
            return _holdingRegisterBuffer;
        }

        /// <summary>
        /// Gets the coils as <see cref="byte"/> array.
        /// </summary>
        public Span<byte> GetCoils()
        {
            return this.GetCoilBuffer();
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
        public Span<byte> GetCoilBuffer(byte unitId)
        {
            return _coilBuffer;
        }

        /// <summary>
        /// Gets the discrete inputs as <see cref="byte"/> array.
        /// </summary>
        public Span<byte> GetDiscreteInputs(byte unitId)
        {
            return this.GetDiscreteInputBuffer();
        }

        /// <summary>
        /// Gets the discrete input buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetDiscreteInputBuffer<T>(byte unitId) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetDiscreteInputBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the discrete input buffer as byte array.
        /// </summary>
        public Span<byte> GetDiscreteInputBuffer(byte unitId)
        {
            return _discreteInputBuffer;
        }

        /// <summary>
        /// Clears all buffer contents.
        /// </summary>
        public void ClearBuffers(byte unitId)
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

        internal void OnRegistersChanged(byte unitId, List<int> registers)
        {
            this.RegistersChanged?.Invoke(this, registers);
        }

        internal void OnCoilsChanged(byte unitId, List<int> coils)
        {
            this.CoilsChanged?.Invoke(this, coils);
        }

        #endregion Methods

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

        #endregion IDisposable Support
    }
}