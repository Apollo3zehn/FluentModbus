using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
    internal abstract class ModbusRequestHandler : IDisposable
    {
        #region Fields

        private Task _task;

        #endregion

        #region Constructors

        public ModbusRequestHandler(ModbusServer modbusServer, int bufferSize)
        {
            this.ModbusServer = modbusServer;
            this.FrameBuffer = new ModbusFrameBuffer(bufferSize);

            this.LastRequest = Stopwatch.StartNew();
            this.IsReady = true;

            this.CTS = new CancellationTokenSource();

            if (this.ModbusServer.IsAsynchronous)
            {
                _task = Task.Run(async () =>
                {
                    while (!this.CTS.IsCancellationRequested)
                    {
                        await this.ReceiveRequestAsync();
                    }
                }, this.CTS.Token);
            }
        }

        #endregion

        #region Properties

        public ModbusServer ModbusServer { get; }
        public Stopwatch LastRequest { get; protected set; }
        public int Length { get; protected set; }
        public bool IsReady { get; protected set; }

        public abstract string DisplayName { get; }

        protected byte UnitIdentifier { get; set; }
        protected CancellationTokenSource CTS { get; }
        protected ModbusFrameBuffer FrameBuffer { get; }

        protected abstract bool IsResponseRequired { get; }

        #endregion

        #region Methods

        public void WriteResponse()
        {
            int frameLength;
            byte rawFunctionCode;

            ModbusFunctionCode functionCode;
            Action processingMethod;

            if (!this.IsResponseRequired)
                return;

            if (!(this.IsReady && this.Length > 0))
                throw new Exception(ErrorMessage.ModbusTcpRequestHandler_NoValidRequestAvailable);

            rawFunctionCode = this.FrameBuffer.Reader.ReadByte();                                              // 07     Function Code

            this.FrameBuffer.Writer.Seek(0, SeekOrigin.Begin);

            if (Enum.IsDefined(typeof(ModbusFunctionCode), rawFunctionCode))
            {
                functionCode = (ModbusFunctionCode)rawFunctionCode;

                try
                {
                    processingMethod = functionCode switch
                    {
                        ModbusFunctionCode.ReadHoldingRegisters => this.ProcessReadHoldingRegisters,
                        ModbusFunctionCode.WriteMultipleRegisters => this.ProcessWriteMultipleRegisters,
                        ModbusFunctionCode.ReadCoils => this.ProcessReadCoils,
                        ModbusFunctionCode.ReadDiscreteInputs => this.ProcessReadDiscreteInputs,
                        ModbusFunctionCode.ReadInputRegisters => this.ProcessReadInputRegisters,
                        ModbusFunctionCode.WriteSingleCoil => this.ProcessWriteSingleCoil,
                        ModbusFunctionCode.WriteSingleRegister => this.ProcessWriteSingleRegister,
                        //ModbusFunctionCode.ReadExceptionStatus
                        //ModbusFunctionCode.WriteMultipleCoils
                        //ModbusFunctionCode.ReadFileRecord
                        //ModbusFunctionCode.WriteFileRecord
                        //ModbusFunctionCode.MaskWriteRegister
                        //ModbusFunctionCode.ReadWriteMultipleRegisters
                        //ModbusFunctionCode.ReadFifoQueue
                        //ModbusFunctionCode.Error
                        _ => () => this.WriteExceptionResponse(rawFunctionCode, ModbusExceptionCode.IllegalFunction)
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
            if (this.ModbusServer.IsAsynchronous)
            {
                lock (this.ModbusServer.Lock)
                {
                    frameLength = this.WriteFrame(processingMethod);
                }
            }
            else
            {
                frameLength = this.WriteFrame(processingMethod);
            }

            this.OnResponseReady(frameLength);
        }

        internal abstract Task ReceiveRequestAsync();

        protected abstract int WriteFrame(Action extendFrame);

        protected abstract void OnResponseReady(int frameLength);

        private void WriteExceptionResponse(ModbusFunctionCode functionCode, ModbusExceptionCode exceptionCode)
        {
            this.WriteExceptionResponse((byte)functionCode, exceptionCode);
        }

        private void WriteExceptionResponse(byte rawFunctionCode, ModbusExceptionCode exceptionCode)
        {
            this.FrameBuffer.Writer.Write((byte)(ModbusFunctionCode.Error + rawFunctionCode));
            this.FrameBuffer.Writer.Write((byte)exceptionCode);
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

            startingAddress = this.FrameBuffer.Reader.ReadUInt16Reverse();
            quantityOfRegisters = this.FrameBuffer.Reader.ReadUInt16Reverse();

            if (this.CheckRegisterBounds(ModbusFunctionCode.ReadHoldingRegisters, startingAddress, this.ModbusServer.MaxHoldingRegisterAddress, quantityOfRegisters, 0x7D))
            {
                this.FrameBuffer.Writer.Write((byte)ModbusFunctionCode.ReadHoldingRegisters);
                this.FrameBuffer.Writer.Write((byte)(quantityOfRegisters * 2));
                this.FrameBuffer.Writer.Write(this.ModbusServer.GetHoldingRegisterBuffer().Slice(startingAddress * 2, quantityOfRegisters * 2).ToArray());
            }
        }

        private void ProcessWriteMultipleRegisters()
        {
            ushort startingAddress;
            ushort quantityOfRegisters;
            byte byteCount;

            startingAddress = this.FrameBuffer.Reader.ReadUInt16Reverse();
            quantityOfRegisters = this.FrameBuffer.Reader.ReadUInt16Reverse();
            byteCount = this.FrameBuffer.Reader.ReadByte();

            if (this.CheckRegisterBounds(ModbusFunctionCode.WriteMultipleRegisters, startingAddress, this.ModbusServer.MaxHoldingRegisterAddress, quantityOfRegisters, 0x7B))
            {
                this.FrameBuffer.Reader.ReadBytes(byteCount).AsSpan().CopyTo(this.ModbusServer.GetHoldingRegisterBuffer().Slice(startingAddress * 2));

                this.FrameBuffer.Writer.Write((byte)ModbusFunctionCode.WriteMultipleRegisters);
                this.FrameBuffer.Writer.WriteReverse(startingAddress);
                this.FrameBuffer.Writer.WriteReverse(quantityOfRegisters);
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

            startingAddress = this.FrameBuffer.Reader.ReadUInt16Reverse();
            quantityOfCoils = this.FrameBuffer.Reader.ReadUInt16Reverse();

            if (this.CheckRegisterBounds(ModbusFunctionCode.ReadCoils, startingAddress, this.ModbusServer.MaxCoilAddress, quantityOfCoils, 0x7D0))
            {
                byteCount = (byte)Math.Ceiling((double)quantityOfCoils / 8);

                coilBuffer = this.ModbusServer.GetCoilBuffer();
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

                this.FrameBuffer.Writer.Write((byte)ModbusFunctionCode.ReadCoils);
                this.FrameBuffer.Writer.Write(byteCount);
                this.FrameBuffer.Writer.Write(targetBuffer);
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

            startingAddress = this.FrameBuffer.Reader.ReadUInt16Reverse();
            quantityOfInputs = this.FrameBuffer.Reader.ReadUInt16Reverse();

            if (this.CheckRegisterBounds(ModbusFunctionCode.ReadDiscreteInputs, startingAddress, this.ModbusServer.MaxInputRegisterAddress, quantityOfInputs, 0x7D0))
            {
                byteCount = (byte)Math.Ceiling((double)quantityOfInputs / 8);

                discreteInputBuffer = this.ModbusServer.GetDiscreteInputBuffer();
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

                this.FrameBuffer.Writer.Write((byte)ModbusFunctionCode.ReadDiscreteInputs);
                this.FrameBuffer.Writer.Write(byteCount);
                this.FrameBuffer.Writer.Write(targetBuffer);
            }
        }

        private void ProcessReadInputRegisters()
        {
            int startingAddress;
            int quantityOfRegisters;

            startingAddress = this.FrameBuffer.Reader.ReadUInt16Reverse();
            quantityOfRegisters = this.FrameBuffer.Reader.ReadUInt16Reverse();

            if (this.CheckRegisterBounds(ModbusFunctionCode.ReadInputRegisters, startingAddress, this.ModbusServer.MaxInputRegisterAddress, quantityOfRegisters, 0x7D))
            {
                this.FrameBuffer.Writer.Write((byte)ModbusFunctionCode.ReadInputRegisters);
                this.FrameBuffer.Writer.Write((byte)(quantityOfRegisters * 2));
                this.FrameBuffer.Writer.Write(this.ModbusServer.GetInputRegisterBuffer().Slice(startingAddress * 2, quantityOfRegisters * 2).ToArray());
            }
        }

        private void ProcessWriteSingleCoil()
        {
            int bufferByteIndex;
            int bufferBitIndex;

            ushort outputAddress;
            ushort outputValue;

            Span<byte> coilBuffer;

            outputAddress = this.FrameBuffer.Reader.ReadUInt16Reverse();
            outputValue = this.FrameBuffer.Reader.ReadUInt16();

            if (this.CheckRegisterBounds(ModbusFunctionCode.WriteSingleCoil, outputAddress, this.ModbusServer.MaxCoilAddress, 1, 1))
            {
                if (outputValue != 0x0000 && outputValue != 0x00FF)
                {
                    this.WriteExceptionResponse(ModbusFunctionCode.ReadHoldingRegisters, ModbusExceptionCode.IllegalDataValue);
                }
                else
                {
                    bufferByteIndex = outputAddress / 8;
                    bufferBitIndex = outputAddress % 8;

                    coilBuffer = this.ModbusServer.GetCoilBuffer();

                    if (outputValue == 0x0000)
                    {
                        coilBuffer[bufferByteIndex] &= (byte)~(1 << bufferBitIndex);
                    }
                    else
                    {
                        coilBuffer[bufferByteIndex] |= (byte)(1 << bufferBitIndex);
                    }

                    this.FrameBuffer.Writer.Write((byte)ModbusFunctionCode.WriteSingleCoil);
                    this.FrameBuffer.Writer.WriteReverse(outputAddress);
                    this.FrameBuffer.Writer.Write(outputValue);
                }
            }
        }

        private void ProcessWriteSingleRegister()
        {
            ushort registerAddress;
            ushort registerValue;

            registerAddress = this.FrameBuffer.Reader.ReadUInt16Reverse();
            registerValue = this.FrameBuffer.Reader.ReadUInt16();

            if (this.CheckRegisterBounds(ModbusFunctionCode.WriteSingleRegister, registerAddress, this.ModbusServer.MaxHoldingRegisterAddress, 1, 1))
            {
                MemoryMarshal.Cast<byte, ushort>(this.ModbusServer.GetHoldingRegisterBuffer())[registerAddress] = registerValue;

                this.FrameBuffer.Writer.Write((byte)ModbusFunctionCode.WriteSingleRegister);
                this.FrameBuffer.Writer.WriteReverse(registerAddress);
                this.FrameBuffer.Writer.Write(registerValue);
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
                    if (this.ModbusServer.IsAsynchronous)
                    {
                        this.CTS?.Cancel();

                        try
                        {
                            _task?.Wait();
                        }
                        catch (Exception ex) when (ex.InnerException.GetType() == typeof(TaskCanceledException))
                        {
                            // Actually, TaskCanceledException is not expected because it is catched in ReceiveRequestAsync() method.
                        }
                    }

                    this.FrameBuffer.Dispose();
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
