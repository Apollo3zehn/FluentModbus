using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ModbusTCP.NET
{
    public class ModbusTcpRequestHandler : IDisposable
    {
        #region Fields

        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private ModbusTcpServer _modbusTcpServer;
        private ExtendedBinaryReader _requestReader;
        private ExtendedBinaryWriter _responseWriter;

        private byte[] _buffer;

        ushort _transactionIdentifier;
        ushort _protocolIdentifier;
        ushort _bytesFollowing;

        byte _unitIdentifier;

        #endregion

        #region Constructors

        public ModbusTcpRequestHandler(TcpClient tcpClient, ModbusTcpServer modbusTcpServer)
        {
            _tcpClient = tcpClient;
            _modbusTcpServer = modbusTcpServer;

            _networkStream = tcpClient.GetStream();

            _buffer = ArrayPool<byte>.Shared.Rent(256);

            _requestReader = new ExtendedBinaryReader(new MemoryStream(_buffer));
            _responseWriter = new ExtendedBinaryWriter(new MemoryStream(_buffer));

            this.LastRequest = Stopwatch.StartNew();
            this.IsReady = true;
            this.IsConnected = true;
        }

        #endregion

        #region Properties

        public Stopwatch LastRequest { get; private set; }
        public int Length { get; private set; }
        public bool IsReady { get; private set; }
        public bool IsConnected { get; private set; }

        #endregion

        #region Methods

        public async void ReceiveRequestAsync()
        {
            int length;
            bool isParsed;

            if (!this.IsConnected)
            {
                return;
            }

            this.IsReady = false;
            this.Length = 0;

            isParsed = false;
            _bytesFollowing = 0;

            try
            {
                while (true)
                {
                    if (_networkStream.DataAvailable)
                    {
                        length = _networkStream.Read(_buffer, 0, _buffer.Length);
                    }
                    else
                    {
                        length = await _networkStream.ReadAsync(_buffer, 0, _buffer.Length);
                    }

                    if (length > 0)
                    {
                        this.Length += length;

                        if (this.Length >= 7)
                        {
                            if (!isParsed)
                            {
                                _requestReader.BaseStream.Seek(0, SeekOrigin.Begin);

                                // read MBAP header
                                _transactionIdentifier = _requestReader.ReadUInt16Reverse();              // 00-01  Transaction Identifier
                                _protocolIdentifier = _requestReader.ReadUInt16Reverse();                 // 02-03  Protocol Identifier               
                                _bytesFollowing = _requestReader.ReadUInt16Reverse();                     // 04-05  Length
                                _unitIdentifier = _requestReader.ReadByte();                              // 06     Unit Identifier

                                if (_protocolIdentifier != 0)
                                {
                                    this.Length = 0;
                                    break;
                                }

                                isParsed = true;
                            }

                            if (this.Length - 6 >= _bytesFollowing)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        this.Length = 0;
                        break;
                    }
                }

                this.LastRequest.Restart();
                this.IsReady = true;
            }
            catch (Exception)
            {
                this.IsConnected = false;
            }
        }

        public void WriteResponse()
        {
            int length;
            byte rawFunctionCode;

            ModbusFunctionCode functionCode;
            Action processingMethod;

            if (!(this.IsReady && this.Length > 0))
            {
                throw new Exception(ErrorMessage.ModbusTcpRequestHandler_NoValidRequestAvailable);
            }

            rawFunctionCode = _requestReader.ReadByte();                                                    // 07     Function Code

            _responseWriter.Seek(0, SeekOrigin.Begin);

            if (Enum.IsDefined(typeof(ModbusFunctionCode), rawFunctionCode))
            {
                functionCode = (ModbusFunctionCode)rawFunctionCode;

                try
                {
                    switch (functionCode)
                    {
                        case ModbusFunctionCode.ReadHoldingRegisters:

                            processingMethod = () => this.ProcessReadHoldingRegisters();
                            break;

                        case ModbusFunctionCode.WriteMultipleRegisters:

                            processingMethod = () => this.ProcessWriteMultipleRegisters();
                            break;

                        case ModbusFunctionCode.ReadCoils:

                            processingMethod = () => this.ProcessReadCoils();
                            break;

                        case ModbusFunctionCode.ReadDiscreteInputs:

                            processingMethod = () => this.ProcessReadDiscreteInputs();
                            break;

                        case ModbusFunctionCode.ReadInputRegisters:

                            processingMethod = () => this.ProcessReadInputRegisters();
                            break;

                        case ModbusFunctionCode.WriteSingleCoil:

                            processingMethod = () => this.ProcessWriteSingleCoil();
                            break;

                        case ModbusFunctionCode.WriteSingleRegister:

                            processingMethod = () => this.ProcessWriteSingleRegister();
                            break;

                        case ModbusFunctionCode.ReadExceptionStatus:
                        case ModbusFunctionCode.WriteMultipleCoils:
                        case ModbusFunctionCode.ReadFileRecord:
                        case ModbusFunctionCode.WriteFileRecord:
                        case ModbusFunctionCode.MaskWriteRegister:
                        case ModbusFunctionCode.ReadWriteMultipleRegisters:
                        case ModbusFunctionCode.ReadFifoQueue:
                        case ModbusFunctionCode.Error:
                        default:

                            processingMethod = () => this.WriteExceptionResponse(rawFunctionCode, ModbusExceptionCode.IllegalFunction);
                            break;
                    }
                }
                catch (Exception)
                {
                    processingMethod = () => this.WriteExceptionResponse(rawFunctionCode, ModbusExceptionCode.ServerDeviceFailure);
                }
            }
            else
            {
                processingMethod = () => this.WriteExceptionResponse(rawFunctionCode, ModbusExceptionCode.IllegalFunction);
            }

            length = this.InternalWriteResponse(processingMethod);

            _networkStream.Write(_buffer, 0, length);
        }

        private int InternalWriteResponse(Action extendFrame)
        {
            int length;

            _responseWriter.Seek(7, SeekOrigin.Begin);
            extendFrame.Invoke();
            length = (int)_responseWriter.BaseStream.Position;

            _responseWriter.Seek(0, SeekOrigin.Begin);
            _responseWriter.WriteReverse(_transactionIdentifier);
            _responseWriter.WriteReverse(_protocolIdentifier);
            _responseWriter.WriteReverse((byte)(length - 6));
            _responseWriter.Write(_unitIdentifier);

            return length;
        }

        private void WriteExceptionResponse(ModbusFunctionCode functionCode, ModbusExceptionCode exceptionCode)
        {
            this.WriteExceptionResponse((byte)functionCode, exceptionCode);
        }

        private void WriteExceptionResponse(byte rawFunctionCode, ModbusExceptionCode exceptionCode)
        {
            _responseWriter.Write((byte)(rawFunctionCode + 0x80));
            _responseWriter.Write((byte)exceptionCode);
        }

        private bool CheckRegisterBounds(int startingAddress, int maxStartingAddress, int quantityOfRegisters, int maxQuantityOfRegisters)
        {
            if (startingAddress < 0 || startingAddress + quantityOfRegisters > maxStartingAddress)
            {
                this.WriteExceptionResponse(ModbusFunctionCode.ReadHoldingRegisters, ModbusExceptionCode.IllegalDataAddress);

                return false;
            }

            if (quantityOfRegisters <= 0 || quantityOfRegisters > maxQuantityOfRegisters)
            {
                this.WriteExceptionResponse(ModbusFunctionCode.ReadHoldingRegisters, ModbusExceptionCode.IllegalDataValue);

                return false;
            }

            return true;
        }

        // class 0
        private void ProcessReadHoldingRegisters()
        {
            int startingAddress;
            int quantityOfRegisters;

            startingAddress = _requestReader.ReadUInt16Reverse();
            quantityOfRegisters = _requestReader.ReadUInt16Reverse();

            if (this.CheckRegisterBounds(startingAddress, _modbusTcpServer.MaxHoldingRegisterAddress, quantityOfRegisters, 0x7D))
            {
                _responseWriter.Write((byte)ModbusFunctionCode.ReadHoldingRegisters);
                _responseWriter.Write((byte)(quantityOfRegisters * 2));
                _responseWriter.Write(_modbusTcpServer.GetHoldingRegisterBuffer().Slice(startingAddress * 2, quantityOfRegisters * 2).ToArray());
            }
        }

        private void ProcessWriteMultipleRegisters()
        {
            int startingAddress;
            int quantityOfRegisters;
            int byteCount;

            startingAddress = _requestReader.ReadUInt16Reverse();
            quantityOfRegisters = _requestReader.ReadUInt16Reverse();
            byteCount = _requestReader.ReadByte();

            if (this.CheckRegisterBounds(startingAddress, _modbusTcpServer.MaxHoldingRegisterAddress, quantityOfRegisters, 0x7B))
            {
                _requestReader.ReadBytes(byteCount).AsSpan().CopyTo(_modbusTcpServer.GetHoldingRegisterBuffer().Slice(startingAddress * 2));

                _responseWriter.Write((byte)ModbusFunctionCode.WriteMultipleRegisters);
                _responseWriter.WriteReverse(startingAddress);
                _responseWriter.WriteReverse(quantityOfRegisters);
            }
        }

        // class 1
        private void ProcessReadCoils()
        {
            int startingAddress;
            int quantityOfCoils;
            int sourceByteIndex;
            int sourceBitIndex;
            int targetByteIndex;
            int targetBitIndex;

            bool isSet;
            byte byteCount;
            byte[] targetBuffer;

            Span<byte> coilBuffer;

            startingAddress = _requestReader.ReadUInt16Reverse();
            quantityOfCoils = _requestReader.ReadUInt16Reverse();

            if (this.CheckRegisterBounds(startingAddress, _modbusTcpServer.MaxCoilAddress, quantityOfCoils, 0x7D0))
            {
                byteCount = (byte)Math.Ceiling((double)quantityOfCoils / 8);

                coilBuffer = _modbusTcpServer.GetCoilBuffer();
                targetBuffer = new byte[byteCount];

                for (int i = 0; i < quantityOfCoils; i++)
                {
                    sourceByteIndex = (startingAddress + i) / 8;
                    sourceBitIndex = (startingAddress + i) % 8;

                    targetByteIndex = i / 8;
                    targetBitIndex = i % 8;

                    isSet = (coilBuffer[sourceByteIndex] & (1 << sourceBitIndex)) > 0;

                    if (isSet)
                    {
                        targetBuffer[targetByteIndex] |= (byte)(1 << targetBitIndex);
                    }
                }

                _responseWriter.Write((byte)ModbusFunctionCode.ReadCoils);
                _responseWriter.Write(byteCount);
                _responseWriter.Write(targetBuffer);
            }
        }

        private void ProcessReadDiscreteInputs()
        {
            int startingAddress;
            int quantityOfInputs;
            int sourceByteIndex;
            int sourceBitIndex;
            int targetByteIndex;
            int targetBitIndex;

            bool isSet;
            byte byteCount;
            byte[] targetBuffer;

            Span<byte> discreteInputBuffer;

            startingAddress = _requestReader.ReadUInt16Reverse();
            quantityOfInputs = _requestReader.ReadUInt16Reverse();

            if (this.CheckRegisterBounds(startingAddress, _modbusTcpServer.MaxInputRegisterAddress, quantityOfInputs, 0x7D0))
            {
                byteCount = (byte)Math.Ceiling((double)quantityOfInputs / 8);

                discreteInputBuffer = _modbusTcpServer.GetDiscreteInputBuffer();
                targetBuffer = new byte[byteCount];

                for (int i = 0; i < quantityOfInputs; i++)
                {
                    sourceByteIndex = (startingAddress + i) / 8;
                    sourceBitIndex = (startingAddress + i) % 8;

                    targetByteIndex = i / 8;
                    targetBitIndex = i % 8;

                    isSet = (discreteInputBuffer[sourceByteIndex] & (1 << sourceBitIndex)) > 0;

                    if (isSet)
                    {
                        targetBuffer[targetByteIndex] |= (byte)(1 << targetBitIndex);
                    }
                }

                _responseWriter.Write((byte)ModbusFunctionCode.ReadDiscreteInputs);
                _responseWriter.Write(byteCount);
                _responseWriter.Write(targetBuffer);
            }
        }

        private void ProcessReadInputRegisters()
        {
            int startingAddress;
            int quantityOfRegisters;

            startingAddress = _requestReader.ReadUInt16Reverse();
            quantityOfRegisters = _requestReader.ReadUInt16Reverse();

            if (this.CheckRegisterBounds(startingAddress, _modbusTcpServer.MaxInputRegisterAddress, quantityOfRegisters, 0x7D))
            {
                _responseWriter.Write((byte)ModbusFunctionCode.ReadInputRegisters);
                _responseWriter.Write((byte)(quantityOfRegisters * 2));
                _responseWriter.Write(_modbusTcpServer.GetInputRegisterBuffer().Slice(startingAddress * 2, quantityOfRegisters * 2).ToArray());
            }
        }

        private void ProcessWriteSingleCoil()
        {
            int outputAddress;
            int bufferByteIndex;
            int bufferBitIndex;
            ushort outputValue;
            Span<byte> coilBuffer;

            outputAddress = _requestReader.ReadUInt16Reverse();
            outputValue = _requestReader.ReadUInt16();

            if (this.CheckRegisterBounds(outputAddress, _modbusTcpServer.MaxCoilAddress, 1, 1))
            {
                if (outputValue != 0x0000 && outputValue != 0x00FF)
                {
                    this.WriteExceptionResponse(ModbusFunctionCode.ReadHoldingRegisters, ModbusExceptionCode.IllegalDataValue);
                }
                else
                {
                    bufferByteIndex = outputAddress / 8;
                    bufferBitIndex = outputAddress % 8;

                    coilBuffer = _modbusTcpServer.GetCoilBuffer();

                    if (outputValue == 0x0000)
                    {
                        coilBuffer[bufferByteIndex] &= (byte)~(1 << bufferBitIndex);
                    }
                    else
                    {
                        coilBuffer[bufferByteIndex] |= (byte)(1 << bufferBitIndex);
                    }

                    _responseWriter.Write((byte)ModbusFunctionCode.WriteSingleCoil);
                    _responseWriter.WriteReverse(outputAddress);
                    _responseWriter.Write(outputValue);
                }
            }
        }

        private void ProcessWriteSingleRegister()
        {
            int registerAddress;
            ushort registerValue;

            registerAddress = _requestReader.ReadUInt16Reverse();
            registerValue = _requestReader.ReadUInt16();

            if (this.CheckRegisterBounds(registerAddress, _modbusTcpServer.MaxHoldingRegisterAddress, 1, 1))
            {
                MemoryMarshal.Cast<byte, ushort>(_modbusTcpServer.GetHoldingRegisterBuffer())[registerAddress] = registerValue;

                _responseWriter.Write((byte)ModbusFunctionCode.WriteSingleRegister);
                _responseWriter.WriteReverse(registerAddress);
                _responseWriter.Write(registerValue);
            }
        }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _tcpClient.Close();

                    _requestReader.Dispose();
                    _responseWriter.Dispose();

                    ArrayPool<byte>.Shared.Return(_buffer);
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
