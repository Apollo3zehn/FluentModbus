using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
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

        private ushort _transactionIdentifier;
        private ushort _protocolIdentifier;
        private ushort _bytesFollowing;

        private byte _unitIdentifier;

        private Task _task;
        private CancellationTokenSource _cts;

        #endregion

        #region Constructors

        public ModbusTcpRequestHandler(TcpClient tcpClient, ModbusTcpServer modbusTcpServer)
        {
            _tcpClient = tcpClient;
            _modbusTcpServer = modbusTcpServer;

            _cts = new CancellationTokenSource();
            _networkStream = tcpClient.GetStream();
            _buffer = ArrayPool<byte>.Shared.Rent(260);

            _cts.Token.Register(() => _networkStream.Close());

            _requestReader = new ExtendedBinaryReader(new MemoryStream(_buffer));
            _responseWriter = new ExtendedBinaryWriter(new MemoryStream(_buffer));

            this.LastRequest = Stopwatch.StartNew();
            this.IsReady = true;

            if (modbusTcpServer.IsAsynchronous)
            {
                _task = Task.Run(async () =>
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        await this.ReceiveRequestAsync();
                    }
                }, _cts.Token);
            }
        }

        #endregion

        #region Properties

        public Stopwatch LastRequest { get; private set; }
        public int Length { get; private set; }
        public bool IsReady { get; private set; }

        #endregion

        #region Methods

        public async Task ReceiveRequestAsync()
        {
            int length;
            bool isParsed;

            if (_cts.IsCancellationRequested)
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
                        // actually, CancellationToken is ignored - therefore: _cts.Token.Register(() => ...);
                        length = await _networkStream.ReadAsync(_buffer, 0, _buffer.Length, _cts.Token);
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
                                this.LastRequest.Restart();
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

                this.IsReady = true;

                if (_modbusTcpServer.IsAsynchronous)
                {
                    this.WriteResponse();
                }
            }
            catch (Exception)
            {
                _cts.Cancel();
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
                    processingMethod = functionCode switch
                    {
                        ModbusFunctionCode.ReadHoldingRegisters => () => this.ProcessReadHoldingRegisters(),
                        ModbusFunctionCode.WriteMultipleRegisters => () => this.ProcessWriteMultipleRegisters(),
                        ModbusFunctionCode.ReadCoils => () => this.ProcessReadCoils(),
                        ModbusFunctionCode.ReadDiscreteInputs => () => this.ProcessReadDiscreteInputs(),
                        ModbusFunctionCode.ReadInputRegisters => () => this.ProcessReadInputRegisters(),
                        ModbusFunctionCode.WriteSingleCoil => () => this.ProcessWriteSingleCoil(),
                        ModbusFunctionCode.WriteSingleRegister => () => this.ProcessWriteSingleRegister(),
                        //ModbusFunctionCode.ReadExceptionStatus
                        //ModbusFunctionCode.WriteMultipleCoils
                        //ModbusFunctionCode.ReadFileRecord
                        //ModbusFunctionCode.WriteFileRecord
                        //ModbusFunctionCode.MaskWriteRegister
                        //ModbusFunctionCode.ReadWriteMultipleRegisters
                        //ModbusFunctionCode.ReadFifoQueue
                        //ModbusFunctionCode.Error
                        _ => (Action)(() => this.WriteExceptionResponse(rawFunctionCode, ModbusExceptionCode.IllegalFunction))
                    };
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

            // if incoming frames shall be processed asynchronously, access to memory must be orchestrated
            if (_modbusTcpServer.IsAsynchronous)
            {
                lock (_modbusTcpServer.Lock)
                {
                    length = this.InternalWriteResponse(processingMethod);
                }
            }
            else
            {
                length = this.InternalWriteResponse(processingMethod);
            }

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
            _responseWriter.Write((byte)(ModbusFunctionCode.Error + rawFunctionCode));
            _responseWriter.Write((byte)exceptionCode);
        }

        private bool CheckRegisterBounds(ModbusFunctionCode functionCode, int startingAddress, int maxStartingAddress, int quantityOfRegisters, int maxQuantityOfRegisters)
        {
            if (startingAddress < 0 || startingAddress + quantityOfRegisters > maxStartingAddress)
            {
                this.WriteExceptionResponse(functionCode, ModbusExceptionCode.IllegalDataAddress);

                return false;
            }

            if (quantityOfRegisters <= 0 || quantityOfRegisters > maxQuantityOfRegisters)
            {
                this.WriteExceptionResponse(functionCode, ModbusExceptionCode.IllegalDataValue);

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

            if (this.CheckRegisterBounds(ModbusFunctionCode.ReadHoldingRegisters, startingAddress, _modbusTcpServer.MaxHoldingRegisterAddress, quantityOfRegisters, 0x7D))
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

            if (this.CheckRegisterBounds(ModbusFunctionCode.WriteMultipleRegisters, startingAddress, _modbusTcpServer.MaxHoldingRegisterAddress, quantityOfRegisters, 0x7B))
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

            if (this.CheckRegisterBounds(ModbusFunctionCode.ReadCoils, startingAddress, _modbusTcpServer.MaxCoilAddress, quantityOfCoils, 0x7D0))
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

            if (this.CheckRegisterBounds(ModbusFunctionCode.ReadDiscreteInputs, startingAddress, _modbusTcpServer.MaxInputRegisterAddress, quantityOfInputs, 0x7D0))
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

            if (this.CheckRegisterBounds(ModbusFunctionCode.ReadInputRegisters, startingAddress, _modbusTcpServer.MaxInputRegisterAddress, quantityOfRegisters, 0x7D))
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

            if (this.CheckRegisterBounds(ModbusFunctionCode.WriteSingleCoil, outputAddress, _modbusTcpServer.MaxCoilAddress, 1, 1))
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

            if (this.CheckRegisterBounds(ModbusFunctionCode.WriteSingleRegister, registerAddress, _modbusTcpServer.MaxHoldingRegisterAddress, 1, 1))
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
                    if (_modbusTcpServer.IsAsynchronous)
                    {
                        _cts?.Cancel();

                        try
                        {
                            _task?.Wait();
                        }
                        catch (Exception ex) when (ex.InnerException.GetType() == typeof(TaskCanceledException))
                        {
                            //
                        }
                    }
                        
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
