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

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a Modbus RTU server with support for holding registers (read and write, 16 bit), input registers (read-only, 16 bit), coils (read and write, 1 bit) and discete inputs (read-only, 1 bit).
        /// </summary>
        /// <param name="unitIdentifier">The unique Modbus RTU unit identifier (1..247).</param>
        public ModbusRtuServer(byte unitIdentifier) : this(unitIdentifier, true)
        {
            //
        }

        /// <summary>
        /// Creates a Modbus RTU server with support for holding registers (read and write, 16 bit), input registers (read-only, 16 bit), coils (read and write, 1 bit) and discete inputs (read-only, 1 bit).
        /// </summary>
        /// <param name="isAsynchronous">Enables or disables the asynchronous operation, where each client request is processed immediately using a locking mechanism. Use synchronuous operation to avoid locks in the hosting application. See the <see href="https://github.com/Apollo3zehn/FluentModbus">documentation</see> for more details.</param>
        /// <param name="unitIdentifier">The unique Modbus RTU unit identifier (1..247).</param>
        public ModbusRtuServer(byte unitIdentifier, bool isAsynchronous) : base(isAsynchronous)
        {
            if (0 < unitIdentifier && unitIdentifier <= 247)
                AddUnit(unitIdentifier);

            else
                throw new ArgumentException(ErrorMessage.ModbusServer_InvalidUnitIdentifier);
        }

        /// <summary>
        /// Creates a multi-unit Modbus RTU server with support for holding registers (read and write, 16 bit), input registers (read-only, 16 bit), coils (read and write, 1 bit) and discete inputs (read-only, 1 bit).
        /// </summary>
        /// <param name="unitIdentifiers">The unique Modbus RTU unit identifiers (1..247).</param>
        public ModbusRtuServer(IEnumerable<byte> unitIdentifiers) : this(unitIdentifiers, true)
        {
            //
        }

        /// <summary>
        /// Creates a multi-unit Modbus RTU server with support for holding registers (read and write, 16 bit), input registers (read-only, 16 bit), coils (read and write, 1 bit) and discete inputs (read-only, 1 bit).
        /// </summary>
        /// <param name="isAsynchronous">Enables or disables the asynchronous operation, where each client request is processed immediately using a locking mechanism. Use synchronuous operation to avoid locks in the hosting application. See the <see href="https://github.com/Apollo3zehn/FluentModbus">documentation</see> for more details.</param>
        /// <param name="unitIdentifiers">The unique Modbus RTU unit identifiers (1..247).</param>
        public ModbusRtuServer(IEnumerable<byte> unitIdentifiers, bool isAsynchronous) : base(isAsynchronous)
        {
            foreach (var unitIdentifier in unitIdentifiers)
            {
                if (0 < unitIdentifier && unitIdentifier <= 247)
                    AddUnit(unitIdentifier);

                else
                    throw new ArgumentException(ErrorMessage.ModbusServer_InvalidUnitIdentifier);
            }
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
        /// Gets or sets the handshaking protocol for serial port transmission of data. Default is Handshake.None.
        /// </summary>
        public Handshake Handshake { get; set; } = Handshake.None;

        /// <summary>
        /// Gets or sets the parity-checking protocol. Default is Parity.Even.
        /// </summary>
        public Parity Parity { get; set; } = Parity.Even;

        /// <summary>
        /// Gets or sets the standard number of stopbits per byte. Default is StopBits.One.
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
            IModbusRtuSerialPort serialPort = ModbusRtuSerialPort.CreateInternal(new SerialPort(port)
            {
                BaudRate = BaudRate,
                Handshake = Handshake,
                Parity = Parity,
                StopBits = StopBits,
                ReadTimeout = ReadTimeout,
                WriteTimeout = WriteTimeout
            });

            _serialPort = serialPort;

            Start(serialPort);
        }

        /// <summary>
        /// Starts the server. It will communicate using the provided <paramref name="serialPort"/>.
        /// </summary>
        /// <param name="serialPort">The serial port to be used.</param>
        public void Start(IModbusRtuSerialPort serialPort)
        {
            /* According to the spec (https://www.modbus.org/docs/Modbus_over_serial_line_V1_02.pdf), 
             * section 2.5.1 RTU Transmission Mode: "... the use of no parity requires 2 stop bits."
             * Remove this check to improve compatibility (#56).
             */

            //if (Parity == Parity.None && StopBits != StopBits.Two)
            //    throw new InvalidOperationException(ErrorMessage.Modbus_NoParityRequiresTwoStopBits);

            base.StopProcessing();
            base.StartProcessing();

            RequestHandler = new ModbusRtuRequestHandler(serialPort, this);
        }

        /// <summary>
        /// Stops the server and closes the underlying serial port.
        /// </summary>
        public void Stop()
        {
            base.StopProcessing();

            RequestHandler?.Dispose();            
        }

        /// <summary>
        /// Dynamically adds a new unit to the server.
        /// </summary>
        /// <param name="unitIdentifer">The identifier of the unit to add.</param>
        public new void AddUnit(byte unitIdentifer)
        {
            base.AddUnit(unitIdentifer);
        }

        /// <summary>
        /// Dynamically removes an existing unit from the server.
        /// </summary>
        /// <param name="unitIdentifer">The identifier of the unit to remove.</param>
        public new void RemoveUnit(byte unitIdentifer)
        {
            base.RemoveUnit(unitIdentifer);
        }

        ///<inheritdoc/>
        protected override void ProcessRequests()
        {
            lock (Lock)
            {
                if (RequestHandler.IsReady)
                {
                    if (RequestHandler.Length > 0)
                        RequestHandler.WriteResponse();

                    _ = RequestHandler.ReceiveRequestAsync();
                }
            }
        }

        #endregion
    }
}
