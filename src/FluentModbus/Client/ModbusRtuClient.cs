using System;
using System.IO;
using System.IO.Ports;

namespace FluentModbus
{
    public class ModbusRtuClient : ModbusClient
    {
        #region Field

        private SerialPort _serialPort;
        private ModbusMessageBuffer _messageBuffer;

        #endregion

        #region Constructors

#warning Check multi threading
#warning Summary.
        /// <summary>
        /// TODO
        /// </summary>
        public ModbusRtuClient()
        {
            //
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the read timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int ReadTimeout { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the write timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int WriteTimeout { get; set; } = 1000;

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

        #endregion

        #region Methods

        /// <summary>
        /// Connect to the specified <paramref name="portName"/>.
        /// </summary>
        /// <param name="portName">The name of the COM port to be used.</param>
        public void Connect(string portName)
        {
#warning Expose SerialPort properties.
            _messageBuffer = new ModbusMessageBuffer(256);

            _serialPort?.Close();
            _serialPort = new SerialPort(portName);

            _serialPort.Open();

            _serialPort.ReadTimeout = this.ReadTimeout;
            _serialPort.WriteTimeout = this.WriteTimeout;
        }

        /// <summary>
        /// Disconnect from the end unit.
        /// </summary>
        public void Disconnect()
        {
            _serialPort?.Close();
            _messageBuffer?.Dispose();
        }

        protected override Span<byte> TransceiveFrame(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame)
        {
            int totalLength;

            byte rawFunctionCode;
            byte newUnitIdentifier;

            ModbusMessageBuffer messageBuffer;
            ExtendedBinaryWriter requestWriter;
            ExtendedBinaryReader responseReader;

            messageBuffer = _messageBuffer;
            requestWriter = _messageBuffer.RequestWriter;
            responseReader = _messageBuffer.ResponseReader;

            // build and send request
            requestWriter.Seek(0, SeekOrigin.Begin);
            requestWriter.Write(unitIdentifier);                                      // 01     Unit Identifier
            extendFrame.Invoke(requestWriter);
#warning Write CRC data

            totalLength = (int)requestWriter.BaseStream.Position;
            _serialPort.Write(messageBuffer.Buffer, 0, totalLength);

            // wait for and process response
            totalLength = 0;
            responseReader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
#warning _serialPort.BaseStream.ReadAsync + Timeout?
                totalLength += _serialPort.Read(messageBuffer.Buffer, totalLength, messageBuffer.Buffer.Length - totalLength);

                if (this.ValidateFrame(messageBuffer.Buffer.AsSpan()[1..totalLength]))
                    break;
            }

            newUnitIdentifier = responseReader.ReadByte();                           // 01     Unit Identifier

            if (unitIdentifier != newUnitIdentifier)
            {
#warning Check that returning unit identifier is same as requested one
                throw new ModbusException(ErrorMessage.ModbusClient_ProtocolIdentifierInvalid);
            }

            rawFunctionCode = responseReader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                this.ProcessError(functionCode, (ModbusExceptionCode)messageBuffer.Buffer[8]);
            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_ResponseFunctionCodeInvalid);

            return messageBuffer.Buffer.AsSpan(1, totalLength - 3);
        }

        private bool ValidateFrame(Span<byte> buffer)
        {
#warning Check this.
            if (buffer.Length < 6)
                return false;

#warning "as  described  in  the  previous  section  the  valid  slave  nodes  addresses  are  in  the  range  of  0  –  247"
            if (buffer[0] < 1 || buffer[0] > 247)
                return false;

#warning Check this.
            // CRC check

            return true;
        }

        #endregion
    }
}
