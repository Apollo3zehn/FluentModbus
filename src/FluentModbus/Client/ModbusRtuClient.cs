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

            ModbusFrameBuffer frameBuffer;
            ExtendedBinaryWriter requestWriter;
            ExtendedBinaryReader responseReader;

            frameBuffer = _frameBuffer;
            requestWriter = _frameBuffer.RequestWriter;
            responseReader = _frameBuffer.ResponseReader;

            // build request
            if (!(1 <= unitIdentifier && unitIdentifier <= 247))
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidUnitIdentifier);

            requestWriter.Seek(0, SeekOrigin.Begin);
            requestWriter.Write(unitIdentifier);                                      // 01     Unit Identifier
            extendFrame.Invoke(requestWriter);
            frameLength = (int)requestWriter.BaseStream.Position;

            // add CRC
            crc = ModbusUtils.CalculateCRC(frameBuffer.Buffer.AsSpan().Slice(0, frameLength));
            requestWriter.Write(crc);
            frameLength = (int)requestWriter.BaseStream.Position;

            // send request
            _serialPort.Write(frameBuffer.Buffer, 0, frameLength);

            // wait for and process response
            frameLength = 0;
            responseReader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                frameLength += _serialPort.Read(frameBuffer.Buffer, frameLength, frameBuffer.Buffer.Length - frameLength);

                if (this.DetectFrame(unitIdentifier, frameBuffer.Buffer.AsSpan().Slice(0, frameLength)))
                    break;
            }

            unitIdentifier = responseReader.ReadByte();
            rawFunctionCode = responseReader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                this.ProcessError(functionCode, (ModbusExceptionCode)frameBuffer.Buffer[8]);
            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

            return frameBuffer.Buffer.AsSpan(1, frameLength - 3);
        }

        private bool DetectFrame(byte unitIdentifier, Span<byte> frame)
        {
            byte newUnitIdentifier;

            /* Correct response frame (min. 6 bytes)
             * 00 Unit Identifier
             * 01 Function Code
             * 02 Byte count
             * 03 Minimum of 1 byte
             * 04 CRC Byte 1
             * 05 CRC Byte 2 
             */

            /* Error response frame (5 bytes)
             * 00 Unit Identifier
             * 01 Function Code + 0x80
             * 02 Exception Code
             * 03 CRC Byte 1
             * 04 CRC Byte 2 
             */

            if (frame.Length < 5)
                return false;

            newUnitIdentifier = frame[0];

            if (newUnitIdentifier != unitIdentifier)
                return false;

            // CRC check
            var crcBytes = frame.Slice(frame.Length - 2, 2);
            var actualCRC = unchecked((ushort)((crcBytes[1] << 8) + crcBytes[0]));
            var expectedCRC = ModbusUtils.CalculateCRC(frame.Slice(0, frame.Length - 2));

            if (actualCRC != expectedCRC)
                return false;

            return true;
        }

#endregion
    }
}
