using System;
using System.IO;
using System.IO.Ports;

namespace FluentModbus
{
    /// <summary>
    /// A Modbu RTU client.
    /// </summary>
    public class ModbusRtuClient : ModbusClient
    {
#warning Implement broadcast mode
#warning Implement ReadExceptionStatus and Diagnostics
#warning Check multi threading

        #region Field

        private SerialPort _serialPort;
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
        /// Connect to the specified <paramref name="portName"/>.
        /// </summary>
        /// <param name="portName">The name of the COM port to be used.</param>
        public void Connect(string portName)
        {
            if (this.Parity == Parity.None && this.StopBits != StopBits.Two)
                throw new InvalidOperationException(ErrorMessage.ModbusClient_NoParityRequiresTwoStopBits);

#warning Alternatively, put this to base class' constructor and implement IDisposable
            _frameBuffer = new ModbusFrameBuffer(256);

            _serialPort?.Close();

            _serialPort = new SerialPort(portName)
            {
                BaudRate = this.BaudRate,
                Handshake = this.Handshake,
                Parity = this.Parity,
                StopBits = this.StopBits,
                ReadTimeout = this.ReadTimeout,
                WriteTimeout = this.WriteTimeout
            };

            _serialPort.Open();
        }

        /// <summary>
        /// Closes the opened COM port and frees all resources.
        /// </summary>
        public void Close()
        {
            _serialPort?.Close();
            _frameBuffer?.Dispose();
        }

        internal protected override Span<byte> TransceiveFrame(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame)
        {
            int frameLength;
            byte rawFunctionCode;
            ushort crc;

            // build request
            if (!(1 <= unitIdentifier && unitIdentifier <= 247))
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidUnitIdentifier);

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

            // wait for and process response
            frameLength = 0;
            _frameBuffer.Reader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                frameLength += _serialPort.Read(_frameBuffer.Buffer, frameLength, _frameBuffer.Buffer.Length - frameLength);

                if (ModbusUtils.DetectFrame(unitIdentifier, _frameBuffer.Buffer.AsSpan().Slice(0, frameLength)))
                    break;
            }

            unitIdentifier = _frameBuffer.Reader.ReadByte();
            rawFunctionCode = _frameBuffer.Reader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                this.ProcessError(functionCode, (ModbusExceptionCode)_frameBuffer.Buffer[8]);
            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

            return _frameBuffer.Buffer.AsSpan(1, frameLength - 3);
        }

        #endregion
    }
}
