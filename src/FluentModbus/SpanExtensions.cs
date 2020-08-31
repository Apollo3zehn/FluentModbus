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
        /// <param name="startingAddress">The Modbus register starting address.</param>
        /// <param name="value">The value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValueLittleEndian<T>(this Span<short> buffer, ushort startingAddress, T value)
            where T : unmanaged
        {
            var byteBuffer = MemoryMarshal
                .AsBytes(buffer)
                .Slice(startingAddress);

            if (!BitConverter.IsLittleEndian)
                value = ModbusUtils.SwitchEndianness(value);

            Unsafe.WriteUnaligned(ref byteBuffer.GetPinnableReference(), value);
        }

        /// <summary>
        /// Writes a single value of type <typeparamref name="T"/> to the registers and converts it to the big-endian representation if necessary.
        /// </summary>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="startingAddress">The Modbus register starting address.</param>
        /// <param name="value">The value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValueBigEndian<T>(this Span<short> buffer, ushort startingAddress, T value)
            where T : unmanaged
        {
            var byteBuffer = MemoryMarshal
                .AsBytes(buffer)
                .Slice(startingAddress);

            if (BitConverter.IsLittleEndian)
                value = ModbusUtils.SwitchEndianness(value);

            Unsafe.WriteUnaligned(ref byteBuffer.GetPinnableReference(), value);
        }

        /// <summary>
        /// Reads a single little-endian value of type <typeparamref name="T"/> from the registers.
        /// </summary>
        /// <typeparam name="T">The type of the value to read.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="startingAddress">The Modbus register starting address.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetValueLittleEndian<T>(this Span<short> buffer, ushort startingAddress)
            where T : unmanaged
        {
            var byteBuffer = MemoryMarshal
                .AsBytes(buffer)
                .Slice(startingAddress);

            var value = Unsafe.ReadUnaligned<T>(ref byteBuffer.GetPinnableReference());

            if (!BitConverter.IsLittleEndian)
                value = ModbusUtils.SwitchEndianness(value);

            return value;
        }

        /// <summary>
        /// Reads a single big-endian value of type <typeparamref name="T"/> from the registers.
        /// </summary>
        /// <typeparam name="T">The type of the value to read.</typeparam>
        /// <param name="buffer">The target buffer.</param>
        /// <param name="startingAddress">The Modbus register starting address.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetValueBigEndian<T>(this Span<short> buffer, ushort startingAddress)
            where T : unmanaged
        {
            var byteBuffer = MemoryMarshal
                .AsBytes(buffer)
                .Slice(startingAddress);

            var value = Unsafe.ReadUnaligned<T>(ref byteBuffer.GetPinnableReference());

            if (BitConverter.IsLittleEndian)
                value = ModbusUtils.SwitchEndianness(value);

            return value;
        }
    }
}
