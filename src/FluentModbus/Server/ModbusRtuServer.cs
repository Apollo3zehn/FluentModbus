using System;
using System.IO.Ports;

namespace FluentModbus
{
    /// <summary>
    /// A Modbus RTU server.
    /// </summary>
    public class ModbusRtuServer : ModbusServer
    {
        #region Fields

        private IModbusRtuSerialPort _serialPort;

        private byte _unitIdentifier;

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
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the unit identifier.
        /// </summary>
        public byte UnitIdentifier
        {
            get
            {
                return _unitIdentifier;
            }
            set
            {
                if (!(1 <= value && value <= 247))
                    throw new Exception(ErrorMessage.ModbusServer_InvalidUnitIdentifier);

                _unitIdentifier = value;
            }
        }

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

            _serialPort = serialPort;

            this.Start(serialPort);
        }
        
        /// <summary>
        /// Stops the server and closes the underlying serial port.
        /// </summary>
        public override void Stop()
        {
            base.Stop();

            this.RequestHandler?.Dispose();            
        }

        internal void Start(IModbusRtuSerialPort serialPort)
        {
            if (this.Parity == Parity.None && this.StopBits != StopBits.Two)
                throw new InvalidOperationException(ErrorMessage.Modbus_NoParityRequiresTwoStopBits);

            // "base..." is important!
            base.Stop();
            base.Start();

            this.RequestHandler = new ModbusRtuRequestHandler(serialPort, this);
        }

        private protected override void ProcessRequests()
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
