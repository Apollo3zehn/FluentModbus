using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if NETSTANDARD2_1_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace FluentModbus;

internal static class ModbusUtils
{
#if NETSTANDARD2_0
    public static bool TryParseEndpoint(ReadOnlySpan<char> value, out IPEndPoint? result)
#endif
#if NETSTANDARD2_1_OR_GREATER
    public static bool TryParseEndpoint(ReadOnlySpan<char> value, [NotNullWhen(true)] out IPEndPoint? result)
#endif
    {
        var addressLength = value.Length;
        var lastColonPos = value.LastIndexOf(':');

        if (lastColonPos > 0)
        {
            if (value[lastColonPos - 1] == ']')
                addressLength = lastColonPos;

            else if (value[..lastColonPos].LastIndexOf(':') == -1)
                addressLength = lastColonPos;
        }

        if (IPAddress.TryParse(value[..addressLength].ToString(), out var address))
        {
            var port = 502U;

            if (addressLength == value.Length ||
                (uint.TryParse(value[(addressLength + 1)..].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out port) && port <= 65536))

            {
                result = new IPEndPoint(address, (int)port);
                return true;
            }
        }

        result = default;

        return false;
    }

    public static ushort CalculateCRC(Memory<byte> buffer)
    {
        var span = buffer.Span;
        ushort crc = 0xFFFF;

        foreach (var value in span)
        {
            crc ^= value;

            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }

        return crc;
    }

    public static bool DetectRequestFrame(byte unitIdentifier, Memory<byte> frame)
    {
#warning This method should be improved by validating the total length against the expected length depending on the function code
        /* Correct response frame (min. 4 bytes)
         * 00 Unit Identifier
         * 01 Function Code
         * (0..x bytes - depends on function code)
         * n-1 CRC Byte 1
         * n   CRC Byte 2
         */

        var span = frame.Span;

        if (span.Length < 4)
            return false;

        if (unitIdentifier != 255) // 255 means "skip unit identifier check"
        {
            var newUnitIdentifier = span[0];

            if (newUnitIdentifier != unitIdentifier)
                return false;
        }

        // CRC check
        var crcBytes = span.Slice(span.Length - 2, 2);
        var actualCRC = unchecked((ushort)((crcBytes[1] << 8) + crcBytes[0]));
        var expectedCRC = CalculateCRC(frame[..^2]);

        if (actualCRC != expectedCRC)
            return false;

        return true;
    }

    public static bool DetectResponseFrame(byte unitIdentifier, Memory<byte> frame)
    {
        // 

        /* Response frame for read methods (0x01, 0x02, 0x03, 0x04, 0x17) (min. 6 bytes)
         * 00 Unit Identifier
         * 01 Function Code
         * 02 Byte count
         * 03 Minimum of 1 byte
         * 04 CRC Byte 1
         * 05 CRC Byte 2
         */

        /* Response frame for write methods (0x05, 0x06, 0x0F, 0x10) (8 bytes)
         * 00 Unit Identifier
         * 01 Function Code
         * 02 Address
         * 03 Address
         * 04 Value
         * 05 Value
         * 06 CRC Byte 1
         * 07 CRC Byte 2
         */

        /* Error response frame (5 bytes)
         * 00 Unit Identifier
         * 01 Function Code + 0x80
         * 02 Exception Code
         * 03 CRC Byte 1
         * 04 CRC Byte 2
         */

        var span = frame.Span;

        // absolute minimum frame size
        if (span.Length < 5)
            return false;

        // 255 means "skip unit identifier check"
        if (unitIdentifier != 255)
        {
            var newUnitIdentifier = span[0];

            if (newUnitIdentifier != unitIdentifier)
                return false;
        }

        // Byte count check
        if (span[1] < 0x80)
        {
            switch (span[1])
            {
                // Read methods
                case 0x01:
                case 0x02:
                case 0x03:
                case 0x04:
                case 0x17:

                    if (span.Length < span[2] + 5)
                        return false;

                    break;

                // Write methods
                case 0x05:
                case 0x06:
                case 0x0F:
                case 0x10:

                    if (span.Length < 8)
                        return false;

                    break;
            }
        }

        // Error (only for completeness, length >= 5 has already been checked above)
        else
        {
            if (span.Length < 5)
                return false;
        }

        // CRC check
        var crcBytes = span.Slice(span.Length - 2, 2);
        var actualCRC = unchecked((ushort)((crcBytes[1] << 8) + crcBytes[0]));
        var expectedCRC = CalculateCRC(frame[..^2]);

        if (actualCRC != expectedCRC)
            return false;

        return true;
    }

    public static short SwitchEndianness(short value)
    {
        var bytes = BitConverter.GetBytes(value);
        return (short)((bytes[0] << 8) + bytes[1]);
    }

    public static ushort SwitchEndianness(ushort value)
    {
        var bytes = BitConverter.GetBytes(value);
        return (ushort)((bytes[0] << 8) + bytes[1]);
    }

    public static T SwitchEndianness<T>(T value) where T : unmanaged
    {
        Span<T> data = stackalloc T[] { value };
        SwitchEndianness(data);

        return data[0];
    }

    public static T ConvertBetweenLittleEndianAndMidLittleEndian<T>(T value) where T : unmanaged
    {
        // from DCBA to CDAB

        if (Unsafe.SizeOf<T>() == 4)
        {
            Span<T> data = stackalloc T[] { value, default };

            var dataset_bytes = MemoryMarshal.Cast<T, byte>(data);
            var offset = 4;

            dataset_bytes[offset + 0] = dataset_bytes[1];
            dataset_bytes[offset + 1] = dataset_bytes[0];
            dataset_bytes[offset + 2] = dataset_bytes[3];
            dataset_bytes[offset + 3] = dataset_bytes[2];

            return data[1];
        }
        else
        {
            throw new Exception($"Type {value.GetType().Name} cannot be represented as mid-little-endian.");
        }
    }

    public static void SwitchEndianness<T>(Memory<T> dataset) where T : unmanaged
    {
        SwitchEndianness(dataset.Span);
    }

    public static void SwitchRegistersBytes(Memory<byte> dataset)
    {
        SwitchRegistersBytes(dataset.Span);
    }

    public static void SwitchRegistersBytes(Span<byte> dataset_bytes)
    {
        for (int i = 0; i < dataset_bytes.Length; i += 2)
        {
            var i1 = i;
            var i2 = i + 1;

            (dataset_bytes[i2], dataset_bytes[i1]) = (dataset_bytes[i1], dataset_bytes[i2]);
        }
    }

    public static void SwitchEndianness<T>(Span<T> dataset) where T : unmanaged
    {
        var size = Marshal.SizeOf<T>();
        var dataset_bytes = MemoryMarshal.Cast<T, byte>(dataset);

        for (int i = 0; i < dataset_bytes.Length; i += size)
        {
            for (int j = 0; j < size / 2; j++)
            {
                var i1 = i + j;
                var i2 = i - j + size - 1;

                (dataset_bytes[i2], dataset_bytes[i1]) = (dataset_bytes[i1], dataset_bytes[i2]);
            }
        }
    }

    /// <summary>
    /// 通用：将 short 寄存器转换为任意 unmanaged 类型（int/float/double/long 等）
    /// </summary>
    public static Span<T> ConvertRegistersTo<T>(Span<short> registers, RegisterOrder registerOrder = RegisterOrder.LowHigh) where T : unmanaged
    {
        if(typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            return MemoryMarshal.Cast<short, T>(registers);
        }

        int bytesPerValue = Marshal.SizeOf<T>();
        if (bytesPerValue % 2 != 0)
        {
            throw new NotSupportedException($"类型 {typeof(T)} 的字节大小不是 2 的倍数，无法由 short 寄存器转换");
        }

        int shortsPerValue = bytesPerValue / 2;
        if (registers.Length % shortsPerValue != 0)
        {
            throw new ArgumentException($"registers 的长度必须是 {shortsPerValue} 的倍数", nameof(registers));
        }

        if (registerOrder == RegisterOrder.HighLow)
        {
            for (int i = 0; i < registers.Length; i += shortsPerValue)
            {
                int left = i;
                int right = i + shortsPerValue - 1;
                while (left < right)
                {
                    (registers[left], registers[right]) = (registers[right], registers[left]);
                    left++;
                    right--;
                }
            }
        }

        return MemoryMarshal.Cast<short, T>(registers);
    }

    /// <summary>
    /// 通用：将任意 unmanaged 类型（int/float/double/long 等）转换为 short 寄存器（支持 RegisterOrder）
    /// </summary>
    public static Span<short> ConvertToRegisters<T>(Span<T> dataset, RegisterOrder registerOrder = RegisterOrder.LowHigh) where T : unmanaged
    {
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            return MemoryMarshal.Cast<T, short>(dataset);
        }

        int bytesPerValue = Marshal.SizeOf<T>();
        if (bytesPerValue % 2 != 0)
        {
            throw new NotSupportedException($"类型 {typeof(T)} 的字节大小不是 2 的倍数，无法转换为 short 寄存器");
        }

        int shortsPerValue = bytesPerValue / 2;
        var shorts2 = MemoryMarshal.Cast<T, short>(dataset);

        if (registerOrder == RegisterOrder.HighLow)
        {
            for (int i = 0; i < shorts2.Length; i += shortsPerValue)
            {
                int left = i;
                int right = i + shortsPerValue - 1;
                while (left < right)
                {
                    (shorts2[left], shorts2[right]) = (shorts2[right], shorts2[left]);
                    left++;
                    right--;
                }
            }
        }
        return shorts2;
    }
}