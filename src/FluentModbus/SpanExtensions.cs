using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FluentModbus
{
    /// <summary>
    /// Contains extension methods to read and write data from the Modbus registers.
    /// </summary>
    public static class SpanExtensions
    {
        /// <summary>
        /// Writes a single value of type <typeparamref name="T"/> to the registers and converts it to the little-endian representation if necessary.
        /// </summary>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="address">The Modbus register address.</param>
        /// <param name="value">The value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetLittleEndian<T>(this Span<short> buffer, int address, T value)
            where T : unmanaged
        {
            if (!(0 <= address && address <= ushort.MaxValue))
                throw new Exception(ErrorMessage.Modbus_InvalidValueUShort);

            var byteBuffer = MemoryMarshal
                .AsBytes(buffer)
                .Slice(address * 2);

            if (!BitConverter.IsLittleEndian)
                value = ModbusUtils.SwitchEndianness(value);

            Unsafe.WriteUnaligned(ref byteBuffer.GetPinnableReference(), value);
        }

        /// <summary>
        /// Writes a single value of type <typeparamref name="T"/> to the registers and converts it to the big-endian representation if necessary.
        /// </summary>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="address">The Modbus register address.</param>
        /// <param name="value">The value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBigEndian<T>(this Span<short> buffer, int address, T value)
            where T : unmanaged
        {
            if (!(0 <= address && address <= ushort.MaxValue))
                throw new Exception(ErrorMessage.Modbus_InvalidValueUShort);

            var byteBuffer = MemoryMarshal
                .AsBytes(buffer)
                .Slice(address * 2);

            if (BitConverter.IsLittleEndian)
                value = ModbusUtils.SwitchEndianness(value);

            Unsafe.WriteUnaligned(ref byteBuffer.GetPinnableReference(), value);
        }

        /// <summary>
        /// Reads a single little-endian value of type <typeparamref name="T"/> from the registers.
        /// </summary>
        /// <typeparam name="T">The type of the value to read.</typeparam>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="address">The Modbus register address.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetLittleEndian<T>(this Span<short> buffer, int address)
            where T : unmanaged
        {
            if (!(0 <= address && address <= ushort.MaxValue))
                throw new Exception(ErrorMessage.Modbus_InvalidValueUShort);

            var byteBuffer = MemoryMarshal
                .AsBytes(buffer)
                .Slice(address * 2);

            var value = Unsafe.ReadUnaligned<T>(ref byteBuffer.GetPinnableReference());

            if (!BitConverter.IsLittleEndian)
                value = ModbusUtils.SwitchEndianness(value);

            return value;
        }

        /// <summary>
        /// Reads a single big-endian value of type <typeparamref name="T"/> from the registers.
        /// </summary>
        /// <typeparam name="T">The type of the value to read.</typeparam>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="address">The Modbus register address.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetBigEndian<T>(this Span<short> buffer, int address)
            where T : unmanaged
        {
            if (!(0 <= address && address <= ushort.MaxValue))
                throw new Exception(ErrorMessage.Modbus_InvalidValueUShort);

            var byteBuffer = MemoryMarshal
                .AsBytes(buffer)
                .Slice(address * 2);

            var value = Unsafe.ReadUnaligned<T>(ref byteBuffer.GetPinnableReference());

            if (BitConverter.IsLittleEndian)
                value = ModbusUtils.SwitchEndianness(value);

            return value;
        }

        /// <summary>
        /// Writes a single bit to the buffer.
        /// </summary>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="address">The Modbus address.</param>
        /// <param name="value">The value to set.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set(this Span<byte> buffer, int address, bool value)
        {
            if (!(0 <= address && address <= ushort.MaxValue))
                throw new Exception(ErrorMessage.Modbus_InvalidValueUShort);

            var byteIndex = address / 8;
            var bitIndex = address % 8;

            // set
            if (value)
                buffer[byteIndex] |= (byte)(1 << bitIndex);

            // clear
            else
                buffer[byteIndex] &= (byte)~(1 << bitIndex);
        }

        /// <summary>
        /// Reads a single bit from the buffer.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="address">The Modbus address.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Get(this Span<byte> buffer, int address)
        {
            if (!(0 <= address && address <= ushort.MaxValue))
                throw new Exception(ErrorMessage.Modbus_InvalidValueUShort);

            var byteIndex = address / 8;
            var bitIndex = address % 8;
            var value = (buffer[byteIndex] & (1 << bitIndex)) > 0;

            return value;
        }

        /// <summary>
        /// Toggles a single bit in the buffer.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="address">The Modbus address.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Toggle(this Span<byte> buffer, int address)
        {
            if (!(0 <= address && address <= ushort.MaxValue))
                throw new Exception(ErrorMessage.Modbus_InvalidValueUShort);

            var byteIndex = address / 8;
            var bitIndex = address % 8;

            buffer[byteIndex] ^= (byte)(1 << bitIndex);
        }
    }
}
