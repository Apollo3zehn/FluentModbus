using System.IO.Ports;

namespace FluentModbus
{
    /// <summary>
    /// A Modbus RTU client.
    /// </summary>
    public partial class ModbusRtuClient : ModbusClient, IDisposable
    {
        #region Field

        private (IModbusRtuSerialPort Value, bool IsInternal)? _serialPort;
        private ModbusFrameBuffer _frameBuffer = default!;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new Modbus RTU client for communication with Modbus RTU servers or bridges, routers and gateways for communication with TCP end units.
        /// </summary>
        public ModbusRtuClient()
        {
            //
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the connection status of the underlying serial port.
        /// </summary>
        public bool IsConnected => _serialPort?.Value.IsOpen ?? false;

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
        // Must be even according to the spec (https://www.modbus.org/docs/Modbus_over_serial_line_V1_02.pdf,
        // section 2.5.1 RTU Transmission Mode): "The default parity mode must be even parity."
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

        #endregion

        #region Methods

        /// <summary>
        /// Connect to the specified <paramref name="port"/> with <see cref="ModbusEndianness.LittleEndian"/> as default byte layout.
        /// </summary>
        /// <param name="port">The COM port to be used, e.g. COM1.</param>
        public void Connect(string port)
        {
            Connect(port, ModbusEndianness.LittleEndian);
        }

        /// <summary>
        /// Connect to the specified <paramref name="port"/>.
        /// </summary>
        /// <param name="port">The COM port to be used, e.g. COM1.</param>
        /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
        public void Connect(string port, ModbusEndianness endianness)
        {
            var serialPort = new ModbusRtuSerialPort(new SerialPort(port)
            {
                BaudRate = BaudRate,
                Handshake = Handshake,
                Parity = Parity,
                StopBits = StopBits,
                ReadTimeout = ReadTimeout,
                WriteTimeout = WriteTimeout
            });

            Initialize(serialPort, isInternal: true, endianness);
        }

        /// <summary>
        /// Initialize the Modbus TCP client with an externally managed <see cref="IModbusRtuSerialPort"/>.
        /// </summary>
        /// <param name="serialPort">The externally managed <see cref="IModbusRtuSerialPort"/>.</param>
        /// <param name="endianness">Specifies the endianness of the data exchanged with the Modbus server.</param>
        public void Initialize(IModbusRtuSerialPort serialPort, ModbusEndianness endianness)
        {
            Initialize(serialPort, isInternal: false, endianness);
        }

        private void Initialize(IModbusRtuSerialPort serialPort, bool isInternal, ModbusEndianness endianness)
        {
            /* According to the spec (https://www.modbus.org/docs/Modbus_over_serial_line_V1_02.pdf), 
             * section 2.5.1 RTU Transmission Mode: "... the use of no parity requires 2 stop bits."
             * Remove this check to improve compatibility (#56).
             */

            //if (Parity == Parity.None && StopBits != StopBits.Two)
            //    throw new InvalidOperationException(ErrorMessage.Modbus_NoParityRequiresTwoStopBits);

            SwapBytes = 
                BitConverter.IsLittleEndian && endianness == ModbusEndianness.BigEndian ||
                !BitConverter.IsLittleEndian && endianness == ModbusEndianness.LittleEndian;

            _frameBuffer = new ModbusFrameBuffer(256);

            if (_serialPort.HasValue && _serialPort.Value.IsInternal)
                _serialPort.Value.Value.Close();

            _serialPort = (serialPort, isInternal);

            if (!serialPort.IsOpen)
                serialPort.Open(); 
        }

        /// <summary>
        /// Closes the opened COM port and frees all resources.
        /// </summary>
        public void Close()
        {
            if (_serialPort.HasValue && _serialPort.Value.IsInternal)
                _serialPort.Value.Value.Close();

            _frameBuffer?.Dispose();
        }

        ///<inheritdoc/>
        protected override Span<byte> TransceiveFrame(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame)
        {
            // WARNING: IF YOU EDIT THIS METHOD, REFLECT ALL CHANGES ALSO IN TransceiveFrameAsync!

            int frameLength;
            byte rawFunctionCode;
            ushort crc;

            // build request
            if (!(0 <= unitIdentifier && unitIdentifier <= 247))
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidUnitIdentifier);

            // special case: broadcast (only for write commands)
            if (unitIdentifier == 0)
            {
                switch (functionCode)
                {
                    case ModbusFunctionCode.WriteMultipleRegisters:
                    case ModbusFunctionCode.WriteSingleCoil:
                    case ModbusFunctionCode.WriteSingleRegister:
                    case ModbusFunctionCode.WriteMultipleCoils:
                    case ModbusFunctionCode.WriteFileRecord:
                    case ModbusFunctionCode.MaskWriteRegister:
                        break;
                    default:
                        throw new ModbusException(ErrorMessage.Modbus_InvalidUseOfBroadcast);
                }
            }

            _frameBuffer.Writer.Seek(0, SeekOrigin.Begin);
            _frameBuffer.Writer.Write(unitIdentifier);                                      // 00     Unit Identifier
            extendFrame(_frameBuffer.Writer);
            frameLength = (int)_frameBuffer.Writer.BaseStream.Position;

            // add CRC
            crc = ModbusUtils.CalculateCRC(_frameBuffer.Buffer.AsMemory()[..frameLength]);
            _frameBuffer.Writer.Write(crc);
            frameLength = (int)_frameBuffer.Writer.BaseStream.Position;

            // send request
            _serialPort!.Value.Value.Write(_frameBuffer.Buffer, 0, frameLength);

            // special case: broadcast (only for write commands)
            if (unitIdentifier == 0)
                return _frameBuffer.Buffer.AsSpan(0, 0);

            // wait for and process response
            frameLength = 0;
            _frameBuffer.Reader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                frameLength += _serialPort!.Value.Value.Read(_frameBuffer.Buffer, frameLength, _frameBuffer.Buffer.Length - frameLength);

                if (ModbusUtils.DetectResponseFrame(unitIdentifier, _frameBuffer.Buffer.AsMemory()[..frameLength]))
                {
                    break;
                }
                
                else
                {
                    // reset length because one or more chunks of data were received and written to
                    // the buffer, but no valid Modbus frame could be detected and now the buffer is full
                    if (frameLength == _frameBuffer.Buffer.Length)
                        frameLength = 0;
                }
            }

            _ = _frameBuffer.Reader.ReadByte();
            rawFunctionCode = _frameBuffer.Reader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                ProcessError(functionCode, (ModbusExceptionCode)_frameBuffer.Buffer[2]);

            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

            return _frameBuffer.Buffer.AsSpan(1, frameLength - 3);
        }

        #endregion

        #region IDisposable

        private bool _disposedValue;

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Close();
                }

                _disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes the current instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
