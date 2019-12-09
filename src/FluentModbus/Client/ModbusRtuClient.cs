using System;
using System.IO;
using System.IO.Ports;

namespace FluentModbus
{
    /// <summary>
    /// A Modbus RTU client.
    /// </summary>
    public class ModbusRtuClient : ModbusClient
    {
        #region Field

        private IModbusRtuSerialPort _serialPort;
        private ModbusFrameBuffer _frameBuffer;

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

        #endregion

        #region Methods

        /// <summary>
        /// Connect to the specified <paramref name="port"/>.
        /// </summary>
        /// <param name="port">The COM port to be used, e.g. COM1.</param>
        public void Connect(string port)
        {
            IModbusRtuSerialPort serialPort;

            serialPort = new ModbusRtuSerialPort(new SerialPort(port)
            {
                BaudRate = this.BaudRate,
                Handshake = this.Handshake,
                Parity = this.Parity,
                StopBits = this.StopBits,
                ReadTimeout = this.ReadTimeout,
                WriteTimeout = this.WriteTimeout
            });

            this.Connect(serialPort);
        }

        /// <summary>
        /// Closes the opened COM port and frees all resources.
        /// </summary>
        public void Close()
        {
            _serialPort?.Close();
            _frameBuffer?.Dispose();
        }

        internal void Connect(IModbusRtuSerialPort serialPort)
        {
            if (this.Parity == Parity.None && this.StopBits != StopBits.Two)
                throw new InvalidOperationException(ErrorMessage.Modbus_NoParityRequiresTwoStopBits);

            _frameBuffer = new ModbusFrameBuffer(256);

            _serialPort?.Close();
            _serialPort = serialPort;
            _serialPort.Open();
        }

        private protected override Span<byte> TransceiveFrame(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame)
        {
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
            crc = ModbusUtils.CalculateCRC(_frameBuffer.Buffer.AsSpan().Slice(0, frameLength));
            _frameBuffer.Writer.Write(crc);
            frameLength = (int)_frameBuffer.Writer.BaseStream.Position;

            // send request
            _serialPort.Write(_frameBuffer.Buffer, 0, frameLength);

            // special case: broadcast (only for write commands)
            if (unitIdentifier == 0)
            {
                return _frameBuffer.Buffer.AsSpan(0, 0);
            }

            // wait for and process response
            frameLength = 0;
            _frameBuffer.Reader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                frameLength += _serialPort.Read(_frameBuffer.Buffer, frameLength, _frameBuffer.Buffer.Length - frameLength);

                if (ModbusUtils.DetectFrame(unitIdentifier, _frameBuffer.Buffer.AsSpan().Slice(0, frameLength)))
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

            unitIdentifier = _frameBuffer.Reader.ReadByte();
            rawFunctionCode = _frameBuffer.Reader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                this.ProcessError(functionCode, (ModbusExceptionCode)_frameBuffer.Buffer[2]);
            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

            return _frameBuffer.Buffer.AsSpan(1, frameLength - 3);
        }

        #endregion
    }
}
