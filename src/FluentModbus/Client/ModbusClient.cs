using System;
using System.Runtime.InteropServices;

namespace FluentModbus
{
    public abstract class ModbusClient
    {
        #region Methods

        protected abstract Span<byte> TransceiveFrame(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame);

        protected void ProcessError(ModbusFunctionCode functionCode, ModbusExceptionCode exceptionCode)
        {
            switch (exceptionCode)
            {
                case ModbusExceptionCode.IllegalFunction:
                    throw new ModbusException(ErrorMessage.ModbusClient_0x01_IllegalFunction);
                case ModbusExceptionCode.IllegalDataAddress:
                    throw new ModbusException(ErrorMessage.ModbusClient_0x02_IllegalDataAddress);
                case ModbusExceptionCode.IllegalDataValue:

                    switch (functionCode)
                    {
                        case ModbusFunctionCode.WriteMultipleRegisters:
                            throw new ModbusException(ErrorMessage.ModbusClient_0x03_IllegalDataValue_0x7B);

                        case ModbusFunctionCode.ReadHoldingRegisters:
                        case ModbusFunctionCode.ReadInputRegisters:
                            throw new ModbusException(ErrorMessage.ModbusClient_0x03_IllegalDataValue_0x7D);

                        case ModbusFunctionCode.ReadCoils:
                        case ModbusFunctionCode.ReadDiscreteInputs:
                            throw new ModbusException(ErrorMessage.ModbusClient_0x03_IllegalDataValue_0x7D0);

                        default:
                            throw new ModbusException(ErrorMessage.ModbusClient_0x03_IllegalDataValue);
                    }

                case ModbusExceptionCode.ServerDeviceFailure:
                    throw new ModbusException(ErrorMessage.ModbusClient_0x04_ServerDeviceFailure);
                case ModbusExceptionCode.Acknowledge:
                    throw new ModbusException(ErrorMessage.ModbusClient_0x05_Acknowledge);
                case ModbusExceptionCode.ServerDeviceBusy:
                    throw new ModbusException(ErrorMessage.ModbusClient_0x06_ServerDeviceBusy);
                case ModbusExceptionCode.MemoryParityError:
                    throw new ModbusException(ErrorMessage.ModbusClient_0x08_MemoryParityError);
                case ModbusExceptionCode.GatewayPathUnavailable:
                    throw new ModbusException(ErrorMessage.ModbusClient_0x0A_GatewayPathUnavailable);
                case ModbusExceptionCode.GatewayTargetDeviceFailedToRespond:
                    throw new ModbusException(ErrorMessage.ModbusClient_0x0B_GatewayTargetDeviceFailedToRespond);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private ushort ConvertSize<T>(ushort count)
        {
            int size;
            ushort quantity;

            size = typeof(T) == typeof(bool) ? 1 : Marshal.SizeOf<T>();
            size = count * size;

            if (size % 2 != 0)
            {
                throw new ArgumentOutOfRangeException(ErrorMessage.ModbusClient_QuantityMustBePositiveInteger);
            }

            quantity = (ushort)(size / 2);

            return quantity;
        }

        #endregion

        // class 0

        /// <summary>
        /// Reads the specified number of values of type <typeparamref name="T"/> from the holding registers.
        /// </summary>
        /// <typeparam name="T">Determines the type of the returned data.</typeparam>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to read.</param>
        public Span<T> ReadHoldingRegisters<T>(byte unitIdentifier, ushort startingAddress, ushort count) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.ReadHoldingRegisters(unitIdentifier, startingAddress, this.ConvertSize<T>(count)));
        }

        /// <summary>
        /// Low level API. Use the generic version of this method for easier access. Reads the specified number of values as byte array from the holding registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="quantity">The number of holding registers (16 bit per register) to read.</param>
        public Span<byte> ReadHoldingRegisters(byte unitIdentifier, ushort startingAddress, ushort quantity)
        {
            Span<byte> buffer;

            buffer = this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.ReadHoldingRegisters, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.ReadHoldingRegisters);              // 07     Function Code
                requestWriter.WriteReverse(startingAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse(quantity);                                            // 10-11  Quantity of Input Registers
            }).Slice(2);

            if (buffer.Length < quantity * 2)
            {
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);
            }

            return buffer;
        }

        /// <summary>
        /// Writes the provided array of type <typeparamref name="T"/> to the holding registers.
        /// </summary>
        /// <typeparam name="T">Determines the type of the provided data.</typeparam>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="dataset">The data of type <typeparamref name="T"/> to write to the server.</param>
        public void WriteMultipleRegisters<T>(byte unitIdentifier, ushort startingAddress, T[] dataset) where T : unmanaged
        {
            this.WriteMultipleRegisters(unitIdentifier, startingAddress, MemoryMarshal.Cast<T, byte>(dataset).ToArray());
        }

        /// <summary>
        /// Low level API. Use the generic version of this method for easier access. Writes the provided byte array to the holding registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="dataset">The byte array to write to the server. A minimum of two bytes is required.</param>
        public void WriteMultipleRegisters(byte unitIdentifier, ushort startingAddress, byte[] dataset)
        {
            if (dataset.Length < 2 || dataset.Length % 2 != 0)
            {
                throw new ArgumentOutOfRangeException(ErrorMessage.ModbusClient_ArrayLengthMustBeGreaterThanTwoAndEven);
            }

            int quantity;

            quantity = dataset.Length / 2;

            this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.WriteMultipleRegisters, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.WriteMultipleRegisters);            // 07     Function Code
                requestWriter.WriteReverse(startingAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse((ushort)quantity);                                    // 10-11  Quantity of Registers
                requestWriter.Write((byte)(quantity * 2));                                       // 12     Byte Count = Quantity of Registers * 2

                requestWriter.Write(dataset, 0, dataset.Length);
            });
        }

        // class 1

        /// <summary>
        /// Reads the specified number of coils as byte array. Each bit of the returned array represents a single coil.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The coil start address for the read operation.</param>
        /// <param name="quantity">The number of coils to read.</param>
        public Span<byte> ReadCoils(byte unitIdentifier, ushort startingAddress, ushort quantity)
        {
            Span<byte> buffer;

            buffer = this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.ReadCoils, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.ReadCoils);                         // 07     Function Code
                requestWriter.WriteReverse(startingAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse(quantity);                                            // 10-11  Quantity of Coils
            }).Slice(2);

            if (buffer.Length < (byte)Math.Ceiling((double)quantity / 8))
            {
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);
            }

            return buffer;
        }

        /// <summary>
        /// Reads the specified number of discrete inputs as byte array. Each bit of the returned array represents a single discete input.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The discrete input start address for the read operation.</param>
        /// <param name="quantity">The number of discrete inputs to read.</param>
        public Span<byte> ReadDiscreteInputs(byte unitIdentifier, ushort startingAddress, ushort quantity)
        {
            Span<byte> buffer;

            buffer = this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.ReadDiscreteInputs, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.ReadDiscreteInputs);                // 07     Function Code
                requestWriter.WriteReverse(startingAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse(quantity);                                            // 10-11  Quantity of Coils
            }).Slice(2);

            if (buffer.Length < (byte)Math.Ceiling((double)quantity / 8))
            {
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);
            }

            return buffer;
        }

        /// <summary>
        /// Reads the specified number of values of type <typeparamref name="T"/> from the input registers.
        /// </summary>
        /// <typeparam name="T">Determines the type of the returned data.</typeparam>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The input register start address for the read operation.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to read.</param>
        public Span<T> ReadInputRegisters<T>(byte unitIdentifier, ushort startingAddress, ushort count) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.ReadInputRegisters(unitIdentifier, startingAddress, this.ConvertSize<T>(count)));
        }

        /// <summary>
        /// Low level API. Use the generic version of this method for easier access. Reads the specified number of values as byte array from the input registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The input register start address for the read operation.</param>
        /// <param name="quantity">The number of input registers (16 bit per register) to read.</param>
        public Span<byte> ReadInputRegisters(byte unitIdentifier, ushort startingAddress, ushort quantity)
        {
            Span<byte> buffer;

            buffer = this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.ReadInputRegisters, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.ReadInputRegisters);                // 07     Function Code
                requestWriter.WriteReverse(startingAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse(quantity);                                            // 10-11  Quantity of Input Registers
            }).Slice(2);

            if (buffer.Length < quantity * 2)
            {
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);
            }

            return buffer;
        }

        /// <summary>
        /// Writes the provided <paramref name="value"/> to the coil registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="registerAddress">The coil address for the write operation.</param>
        /// <param name="value">The value to write to the server.</param>
        public void WriteSingleCoil(byte unitIdentifier, ushort registerAddress, bool value)
        {
            this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.WriteSingleCoil, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.WriteSingleCoil);                   // 07     Function Code
                requestWriter.WriteReverse(registerAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse((ushort)(value ? 0xFF00 : 0x0000));                   // 10-11  Value
            });
        }

        /// <summary>
        /// Writes the provided <paramref name="value"/> to the holding registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="registerAddress">The holding register address for the write operation.</param>
        /// <param name="value">The value to write to the server.</param>
        public void WriteSingleRegister(byte unitIdentifier, ushort registerAddress, short value)
        {
            this.WriteSingleRegister(unitIdentifier, registerAddress, MemoryMarshal.Cast<short, byte>(new [] { value }).ToArray());
        }

        /// <summary>
        /// Writes the provided <paramref name="value"/> to the holding registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="registerAddress">The holding register address for the write operation.</param>
        /// <param name="value">The value to write to the server.</param>
        public void WriteSingleRegister(byte unitIdentifier, ushort registerAddress, ushort value)
        {
            this.WriteSingleRegister(unitIdentifier, registerAddress, MemoryMarshal.Cast<ushort, byte>(new[] { value }).ToArray());
        }

        /// <summary>
        /// Low level API. Use the overloads of this method for easier access. Writes the provided byte array to the holding register.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="registerAddress">The holding register address for the write operation.</param>
        /// <param name="value">The value to write to the server, which is passed as a 2-byte array.</param>
        public void WriteSingleRegister(byte unitIdentifier, ushort registerAddress, byte[] value)
        {
            if (value.Length != 2)
            {
                throw new ArgumentOutOfRangeException(ErrorMessage.ModbusClient_ArrayLengthMustBeEqualToTwo);
            };

            this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.WriteSingleRegister, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.WriteSingleRegister);               // 07     Function Code
                requestWriter.WriteReverse(registerAddress);                                     // 08-09  Starting Address
                requestWriter.Write(value);                                                      // 10-11  Value
            });
        }

        // class 2
        [Obsolete("This method is not implemented.")]
        public void WriteMultipleCoils()
        {
            throw new NotImplementedException();
        }

        [Obsolete("This method is not implemented.")]
        public void ReadFileRecord()
        {
            throw new NotImplementedException();
        }

        [Obsolete("This method is not implemented.")]
        public void WriteFileRecord()
        {
            throw new NotImplementedException();
        }

        [Obsolete("This method is not implemented.")]
        public void MaskWriteRegister()
        {
            throw new NotImplementedException();
        }

        [Obsolete("This method is not implemented.")]
        public void ReadWriteMultipleRegisters()
        {
            throw new NotImplementedException();
        }

        [Obsolete("This method is not implemented.")]
        public void ReadFifoQueue()
        {
            throw new NotImplementedException();
        }
    }
}
