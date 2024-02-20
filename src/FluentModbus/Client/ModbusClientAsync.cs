
 /* This is automatically translated code. */

#pragma warning disable CS1998

using System.Runtime.InteropServices;

namespace FluentModbus
{
    public abstract partial class ModbusClient
    {
        /// <summary>
        /// Sends the requested modbus message and waits for the response.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier.</param>
        /// <param name="functionCode">The function code.</param>
        /// <param name="extendFrame">An action to be called to extend the prepared Modbus frame with function code specific data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        protected abstract Task<Memory<byte>> TransceiveFrameAsync(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the specified number of values of type <typeparamref name="T"/> from the holding registers.
        /// </summary>
        /// <typeparam name="T">Determines the type of the returned data.</typeparam>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to read.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task<Memory<T>> ReadHoldingRegistersAsync<T>(int unitIdentifier, int startingAddress, int count, CancellationToken cancellationToken = default) where T : unmanaged
        {
            var unitIdentifier_converted = ConvertUnitIdentifier(unitIdentifier);
            var startingAddress_converted = ConvertUshort(startingAddress);
            var count_converted = ConvertUshort(count);

            var dataset = SpanExtensions.Cast<byte, T>(await 
                ReadHoldingRegistersAsync(unitIdentifier_converted, startingAddress_converted, ConvertSize<T>(count_converted)).ConfigureAwait(false));

            if (SwapBytes)
                ModbusUtils.SwitchEndianness(dataset);

            return dataset;
        }

        /// <summary>
        /// Low level API. Use the generic version of this method for easier access. Reads the specified number of values as byte array from the holding registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the read operation.</param>
        /// <param name="quantity">The number of holding registers (16 bit per register) to read.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task<Memory<byte>> ReadHoldingRegistersAsync(byte unitIdentifier, ushort startingAddress, ushort quantity, CancellationToken cancellationToken = default)
        {
            var buffer = (await TransceiveFrameAsync(unitIdentifier, ModbusFunctionCode.ReadHoldingRegisters, writer =>
            {
                writer.Write((byte)ModbusFunctionCode.ReadHoldingRegisters);              // 07     Function Code
                
                if (BitConverter.IsLittleEndian)
                {
                    writer.WriteReverse(startingAddress);                                 // 08-09  Starting Address
                    writer.WriteReverse(quantity);                                        // 10-11  Quantity of Input Registers
                }
                else
                {
                    writer.Write(startingAddress);                                        // 08-09  Starting Address
                    writer.Write(quantity);                                               // 10-11  Quantity of Input Registers
                }
            }, cancellationToken).ConfigureAwait(false)).Slice(2);

            if (buffer.Length < quantity * 2)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);

            return buffer;
        }

        /// <summary>
        /// Writes the provided array of type <typeparamref name="T"/> to the holding registers.
        /// </summary>
        /// <typeparam name="T">Determines the type of the provided data.</typeparam>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the write operation.</param>
        /// <param name="dataset">The data of type <typeparamref name="T"/> to write to the server.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task WriteMultipleRegistersAsync<T>(int unitIdentifier, int startingAddress, T[] dataset, CancellationToken cancellationToken = default) where T : unmanaged
        {
            var unitIdentifier_converted = ConvertUnitIdentifier(unitIdentifier);
            var startingAddress_converted = ConvertUshort(startingAddress);

            if (SwapBytes)
                ModbusUtils.SwitchEndianness(dataset.AsSpan());

            await WriteMultipleRegistersAsync(unitIdentifier_converted, startingAddress_converted, MemoryMarshal.Cast<T, byte>(dataset).ToArray(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Low level API. Use the generic version of this method for easier access. Writes the provided byte array to the holding registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The holding register start address for the write operation.</param>
        /// <param name="dataset">The byte array to write to the server. A minimum of two bytes is required.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task WriteMultipleRegistersAsync(byte unitIdentifier, ushort startingAddress, byte[] dataset, CancellationToken cancellationToken = default)
        {
            if (dataset.Length < 2 || dataset.Length % 2 != 0)
                throw new ArgumentOutOfRangeException(ErrorMessage.ModbusClient_ArrayLengthMustBeGreaterThanTwoAndEven);

            var quantity = dataset.Length / 2;

            await TransceiveFrameAsync(unitIdentifier, ModbusFunctionCode.WriteMultipleRegisters, writer =>
            {
                writer.Write((byte)ModbusFunctionCode.WriteMultipleRegisters);            // 07     Function Code

                if (BitConverter.IsLittleEndian)
                {
                    writer.WriteReverse(startingAddress);                                 // 08-09  Starting Address
                    writer.WriteReverse((ushort)quantity);                                // 10-11  Quantity of Registers

                }
                else
                {
                    writer.Write(startingAddress);                                        // 08-09  Starting Address
                    writer.Write((ushort)quantity);                                       // 10-11  Quantity of Registers

                }

                writer.Write((byte)(quantity * 2));                                       // 12     Byte Count = Quantity of Registers * 2

                writer.Write(dataset, 0, dataset.Length);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads the specified number of coils as byte array. Each bit of the returned array represents a single coil.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The coil start address for the read operation.</param>
        /// <param name="quantity">The number of coils to read.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task<Memory<byte>> ReadCoilsAsync(int unitIdentifier, int startingAddress, int quantity, CancellationToken cancellationToken = default)
        {
            var unitIdentifier_converted = ConvertUnitIdentifier(unitIdentifier);
            var startingAddress_converted = ConvertUshort(startingAddress);
            var quantity_converted = ConvertUshort(quantity);

            var buffer = (await TransceiveFrameAsync(unitIdentifier_converted, ModbusFunctionCode.ReadCoils, writer =>
            {
                writer.Write((byte)ModbusFunctionCode.ReadCoils);                         // 07     Function Code

                if (BitConverter.IsLittleEndian)
                {
                    writer.WriteReverse(startingAddress_converted);                       // 08-09  Starting Address
                    writer.WriteReverse(quantity_converted);                              // 10-11  Quantity of Coils
                }
                else
                {
                    writer.Write(startingAddress_converted);                              // 08-09  Starting Address
                    writer.Write(quantity_converted);                                     // 10-11  Quantity of Coils
                }
            }, cancellationToken).ConfigureAwait(false)).Slice(2);

            if (buffer.Length < (byte)Math.Ceiling((double)quantity_converted / 8))
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);

            return buffer;
        }

        /// <summary>
        /// Reads the specified number of discrete inputs as byte array. Each bit of the returned array represents a single discrete input.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The discrete input start address for the read operation.</param>
        /// <param name="quantity">The number of discrete inputs to read.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task<Memory<byte>> ReadDiscreteInputsAsync(int unitIdentifier, int startingAddress, int quantity, CancellationToken cancellationToken = default)
        {
            var unitIdentifier_converted = ConvertUnitIdentifier(unitIdentifier);
            var startingAddress_converted = ConvertUshort(startingAddress);
            var quantity_converted = ConvertUshort(quantity);

            var buffer = (await TransceiveFrameAsync(unitIdentifier_converted, ModbusFunctionCode.ReadDiscreteInputs, writer =>
            {
                writer.Write((byte)ModbusFunctionCode.ReadDiscreteInputs);                // 07     Function Code

                if (BitConverter.IsLittleEndian)
                {
                    writer.WriteReverse(startingAddress_converted);                       // 08-09  Starting Address
                    writer.WriteReverse(quantity_converted);                              // 10-11  Quantity of Coils
                }
                else
                {
                    writer.Write(startingAddress_converted);                              // 08-09  Starting Address
                    writer.Write(quantity_converted);                                     // 10-11  Quantity of Coils
                }
            }, cancellationToken).ConfigureAwait(false)).Slice(2);

            if (buffer.Length < (byte)Math.Ceiling((double)quantity_converted / 8))
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);

            return buffer;
        }

        /// <summary>
        /// Reads the specified number of values of type <typeparamref name="T"/> from the input registers.
        /// </summary>
        /// <typeparam name="T">Determines the type of the returned data.</typeparam>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The input register start address for the read operation.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to read.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task<Memory<T>> ReadInputRegistersAsync<T>(int unitIdentifier, int startingAddress, int count, CancellationToken cancellationToken = default) where T : unmanaged
        {
            var unitIdentifier_converted = ConvertUnitIdentifier(unitIdentifier);
            var startingAddress_converted = ConvertUshort(startingAddress);
            var count_converted = ConvertUshort(count);

            var dataset = SpanExtensions.Cast<byte, T>(await 
                ReadInputRegistersAsync(unitIdentifier_converted, startingAddress_converted, ConvertSize<T>(count_converted)).ConfigureAwait(false));

            if (SwapBytes)
                ModbusUtils.SwitchEndianness(dataset);

            return dataset;
        }

        /// <summary>
        /// Low level API. Use the generic version of this method for easier access. Reads the specified number of values as byte array from the input registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="startingAddress">The input register start address for the read operation.</param>
        /// <param name="quantity">The number of input registers (16 bit per register) to read.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task<Memory<byte>> ReadInputRegistersAsync(byte unitIdentifier, ushort startingAddress, ushort quantity, CancellationToken cancellationToken = default)
        {
            var buffer = (await TransceiveFrameAsync(unitIdentifier, ModbusFunctionCode.ReadInputRegisters, writer =>
            {
                writer.Write((byte)ModbusFunctionCode.ReadInputRegisters);                // 07     Function Code

                if (BitConverter.IsLittleEndian)
                {
                    writer.WriteReverse(startingAddress);                                 // 08-09  Starting Address
                    writer.WriteReverse(quantity);                                        // 10-11  Quantity of Input Registers
                }
                else
                {
                    writer.Write(startingAddress);                                        // 08-09  Starting Address
                    writer.Write(quantity);                                               // 10-11  Quantity of Input Registers
                }
            }, cancellationToken).ConfigureAwait(false)).Slice(2);

            if (buffer.Length < quantity * 2)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);

            return buffer;
        }

        /// <summary>
        /// Writes the provided <paramref name="value"/> to the coil registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="registerAddress">The coil address for the write operation.</param>
        /// <param name="value">The value to write to the server.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task WriteSingleCoilAsync(int unitIdentifier, int registerAddress, bool value, CancellationToken cancellationToken = default)
        {
            var unitIdentifier_converted = ConvertUnitIdentifier(unitIdentifier);
            var registerAddress_converted = ConvertUshort(registerAddress);

            await TransceiveFrameAsync(unitIdentifier_converted, ModbusFunctionCode.WriteSingleCoil, writer =>
            {
                writer.Write((byte)ModbusFunctionCode.WriteSingleCoil);                   // 07     Function Code

                if (BitConverter.IsLittleEndian)
                {
                    writer.WriteReverse(registerAddress_converted);                       // 08-09  Starting Address
                    writer.WriteReverse((ushort)(value ? 0xFF00 : 0x0000));               // 10-11  Value
                }
                else
                {
                    writer.Write(registerAddress_converted);                              // 08-09  Starting Address
                    writer.Write((ushort)(value ? 0xFF00 : 0x0000));                      // 10-11  Value
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes the provided <paramref name="value"/> to the holding registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="registerAddress">The holding register address for the write operation.</param>
        /// <param name="value">The value to write to the server.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task WriteSingleRegisterAsync(int unitIdentifier, int registerAddress, short value, CancellationToken cancellationToken = default)
        {
            var unitIdentifier_converted = ConvertUnitIdentifier(unitIdentifier);
            var registerAddress_converted = ConvertUshort(registerAddress);

            if (SwapBytes)
                value = ModbusUtils.SwitchEndianness(value);

            await WriteSingleRegisterAsync(unitIdentifier_converted, registerAddress_converted, MemoryMarshal.Cast<short, byte>(new [] { value }).ToArray(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes the provided <paramref name="value"/> to the holding registers.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="registerAddress">The holding register address for the write operation.</param>
        /// <param name="value">The value to write to the server.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task WriteSingleRegisterAsync(int unitIdentifier, int registerAddress, ushort value, CancellationToken cancellationToken = default)
        {
            var unitIdentifier_converted = ConvertUnitIdentifier(unitIdentifier);
            var registerAddress_converted = ConvertUshort(registerAddress);

            if (SwapBytes)
                value = ModbusUtils.SwitchEndianness(value);

            await WriteSingleRegisterAsync(unitIdentifier_converted, registerAddress_converted, MemoryMarshal.Cast<ushort, byte>(new[] { value }).ToArray(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Low level API. Use the overloads of this method for easier access. Writes the provided byte array to the holding register.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="registerAddress">The holding register address for the write operation.</param>
        /// <param name="value">The value to write to the server, which is passed as a 2-byte array.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task WriteSingleRegisterAsync(byte unitIdentifier, ushort registerAddress, byte[] value, CancellationToken cancellationToken = default)
        {
            if (value.Length != 2)
                throw new ArgumentOutOfRangeException(ErrorMessage.ModbusClient_ArrayLengthMustBeEqualToTwo);

            await TransceiveFrameAsync(unitIdentifier, ModbusFunctionCode.WriteSingleRegister, writer =>
            {
                writer.Write((byte)ModbusFunctionCode.WriteSingleRegister);               // 07     Function Code

                if (BitConverter.IsLittleEndian)
                    writer.WriteReverse(registerAddress);                                 // 08-09  Starting Address
                else
                    writer.Write(registerAddress);                                        // 08-09  Starting Address

                writer.Write(value);                                                      // 10-11  Value
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// This methdod is not implemented.
        /// </summary>
        [Obsolete("This method is not implemented.")]
        public async Task WriteMultipleCoilsAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This methdod is not implemented.
        /// </summary>
        [Obsolete("This method is not implemented.")]
        public async Task ReadFileRecordAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This methdod is not implemented.
        /// </summary>
        [Obsolete("This method is not implemented.")]
        public async Task WriteFileRecordAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This methdod is not implemented.
        /// </summary>
        [Obsolete("This method is not implemented.")]
        public async Task MaskWriteRegisterAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reads the specified number of values of type <typeparamref name="TRead"/> from and writes the provided array of type <typeparamref name="TWrite"/> to the holding registers. The write operation is performed before the read.
        /// </summary>
        /// <typeparam name="TRead">Determines the type of the returned data.</typeparam>
        /// <typeparam name="TWrite">Determines the type of the provided data.</typeparam>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="readStartingAddress">The holding register start address for the read operation.</param>
        /// <param name="readCount">The number of elements of type <typeparamref name="TRead"/> to read.</param>
        /// <param name="writeStartingAddress">The holding register start address for the write operation.</param>
        /// <param name="dataset">The data of type <typeparamref name="TWrite"/> to write to the server.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task<Memory<TRead>> ReadWriteMultipleRegistersAsync<TRead, TWrite>(int unitIdentifier, int readStartingAddress, int readCount, int writeStartingAddress, TWrite[] dataset, CancellationToken cancellationToken = default) where TRead : unmanaged
                                                                                                                                                                             where TWrite : unmanaged
        {
            var unitIdentifier_converted = ConvertUnitIdentifier(unitIdentifier);
            var readStartingAddress_converted = ConvertUshort(readStartingAddress);
            var readCount_converted = ConvertUshort(readCount);
            var writeStartingAddress_converted = ConvertUshort(writeStartingAddress);

            if (SwapBytes)
                ModbusUtils.SwitchEndianness(dataset.AsSpan());

            var readQuantity = ConvertSize<TRead>(readCount_converted);
            var byteData = MemoryMarshal.Cast<TWrite, byte>(dataset).ToArray();

            var dataset2 = SpanExtensions.Cast<byte, TRead>(await ReadWriteMultipleRegistersAsync(unitIdentifier_converted, readStartingAddress_converted, readQuantity, writeStartingAddress_converted, byteData).ConfigureAwait(false));

            if (SwapBytes)
                ModbusUtils.SwitchEndianness(dataset2);

            return dataset2;
        }

        /// <summary>
        /// Low level API. Use the generic version of this method for easier access. Reads the specified number of values as byte array from and writes the provided byte array to the holding registers. The write operation is performed before the read.
        /// </summary>
        /// <param name="unitIdentifier">The unit identifier is used to communicate via devices such as bridges, routers and gateways that use a single IP address to support multiple independent Modbus end units. Thus, the unit identifier is the address of a remote slave connected on a serial line or on other buses. Use the default values 0x00 or 0xFF when communicating to a Modbus server that is directly connected to a TCP/IP network.</param>
        /// <param name="readStartingAddress">The holding register start address for the read operation.</param>
        /// <param name="readQuantity">The number of holding registers (16 bit per register) to read.</param>
        /// <param name="writeStartingAddress">The holding register start address for the write operation.</param>
        /// <param name="dataset">The byte array to write to the server. A minimum of two bytes is required.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        public async Task<Memory<byte>> ReadWriteMultipleRegistersAsync(byte unitIdentifier, ushort readStartingAddress, ushort readQuantity, ushort writeStartingAddress, byte[] dataset, CancellationToken cancellationToken = default)
        {
            if (dataset.Length < 2 || dataset.Length % 2 != 0)
                throw new ArgumentOutOfRangeException(ErrorMessage.ModbusClient_ArrayLengthMustBeGreaterThanTwoAndEven);

            var writeQuantity = dataset.Length / 2;

            var buffer = (await TransceiveFrameAsync(unitIdentifier, ModbusFunctionCode.ReadWriteMultipleRegisters, writer =>
            {
                writer.Write((byte)ModbusFunctionCode.ReadWriteMultipleRegisters);      // 07     Function Code

                if (BitConverter.IsLittleEndian)
                {
                    writer.WriteReverse(readStartingAddress);                           // 08-09  Read Starting Address
                    writer.WriteReverse(readQuantity);                                  // 10-11  Quantity to Read
                    writer.WriteReverse(writeStartingAddress);                          // 12-13  Read Starting Address
                    writer.WriteReverse((ushort)writeQuantity);                         // 14-15  Quantity to Write
                }
                else
                {
                    writer.Write(readStartingAddress);                                  // 08-09  Read Starting Address
                    writer.Write(readQuantity);                                         // 10-11  Quantity to Read
                    writer.Write(writeStartingAddress);                                 // 12-13  Read Starting Address
                    writer.Write((ushort)writeQuantity);                                // 14-15  Quantity to Write
                }
                
                writer.Write((byte)(writeQuantity * 2));                                // 16     Byte Count = Quantity to Write * 2

                writer.Write(dataset, 0, dataset.Length);
            }, cancellationToken).ConfigureAwait(false)).Slice(2);

            if (buffer.Length < readQuantity * 2)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);

            return buffer;
        }

        /// <summary>
        /// This methdod is not implemented.
        /// </summary>
        [Obsolete("This method is not implemented.")]
        public async Task ReadFifoQueueAsync()
        {
            throw new NotImplementedException();
        }

	}
}

#pragma warning restore CS1998