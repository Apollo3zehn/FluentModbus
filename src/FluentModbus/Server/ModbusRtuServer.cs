using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
    /// <summary>
    /// A Modbus RTU server.
    /// </summary>
    public class ModbusRtuServer : ModbusServer
    {
        #region Fields

        private IModbusRtuSerialPort _serialPort;
        private Task _task_process_requests;
        private ManualResetEventSlim _manualResetEvent;
        private CancellationTokenSource _cts;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a Modbus RTU server with support for holding registers (read and write, 16 bit), input registers (read-only, 16 bit), coils (read and write, 1 bit) and discete inputs (read-only, 1 bit).
        /// </summary>
        /// <param name="unitIdentifier">The unique Modbus RTU unit identifier.</param>
        public ModbusRtuServer(byte unitIdentifier) : this(unitIdentifier, true)
        {
            //
        }

        /// <summary>
        /// Creates a Modbus RTU server with support for holding registers (read and write, 16 bit), input registers (read-only, 16 bit), coils (read and write, 1 bit) and discete inputs (read-only, 1 bit).
        /// </summary>
        /// <param name="isAsynchronous">Enables or disables the asynchronous operation, where each client request is processed immediately using a locking mechanism. Use synchronuous operation to avoid locks in the hosting application. See the <see href="https://github.com/Apollo3zehn/FluentModbus">documentation</see> for more details.</param>
        /// <param name="unitIdentifier">The unique Modbus RTU unit identifier.</param>
        public ModbusRtuServer(byte unitIdentifier, bool isAsynchronous) : base(isAsynchronous)
        {
            this.UnitIdentifier = unitIdentifier;

            _manualResetEvent = new ManualResetEventSlim(false);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the connection status of the underlying serial port.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return _serialPort != null ? _serialPort.IsOpen : false;
            }
        }

        /// <summary>
        /// Gets or sets the serial baud rate. Default is 9600.
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// Gets or sets the handshaking protocol for serial port transmission of data. Default is <see cref="Handshake.None"/>.
        /// </summary>
        public Handshake Handshake { get; set; } = Handshake.None;

        /// <summary>
        /// Gets or sets the parity-checking protocol. Default is <see cref="Parity.Even"/>.
        /// </summary>
        public Parity Parity { get; set; } = Parity.Even;

        /// <summary>
        /// Gets or sets the standard number of stopbits per byte. Default is <see cref="StopBits.One"/>.
        /// </summary>
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>
        /// Gets or sets the read timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int ReadTimeout { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the write timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int WriteTimeout { get; set; } = 1000;

        internal ModbusRtuRequestHandler RequestHandler { get; private set; }

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

            this.Start(serialPort);
        }
        
        internal void Start(IModbusRtuSerialPort serialPort)
        {
            this.Stop();

            _serialPort = serialPort;
            _serialPort.Open();

            _cts = new CancellationTokenSource();

            this.RequestHandler = new ModbusRtuRequestHandler(_serialPort, this);

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
        /// Stops the server and closes the underlying serial port.
        /// </summary>
        public override void Stop()
        {
            _cts?.Cancel();
            _manualResetEvent?.Set();

            try
            {
                _task_process_requests?.Wait();
            }
            catch (Exception ex) when (ex.InnerException.GetType() == typeof(TaskCanceledException))
            {
                //
            }

            _serialPort?.Close();
            this.RequestHandler?.Dispose();
            this.ClearBuffers();
        }

        /// <summary>
        /// Serves an available client request. For synchronous operation only.
        /// </summary>
        public void Update()
        {
            if (this.IsAsynchronous || !this.IsReady)
                return;

            _manualResetEvent.Set();
        }

        private void ProcessRequests()
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

        #endregion
    }
}
