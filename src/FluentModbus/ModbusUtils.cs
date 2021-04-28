using System;
using System.Runtime.InteropServices;

namespace FluentModbus
{
    internal static class ModbusUtils
    {
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

        public static bool DetectFrame(byte unitIdentifier, Memory<byte> frame)
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

            var span = frame.Span;

            if (span.Length < 5)
                return false;

            if (unitIdentifier != 255) // 255 means "skip unit identifier check"
            {
                newUnitIdentifier = span[0];

                if (newUnitIdentifier != unitIdentifier)
                    return false;
            }

            // CRC check
            var crcBytes = span.Slice(span.Length - 2, 2);
            var actualCRC = unchecked((ushort)((crcBytes[1] << 8) + crcBytes[0]));
            var expectedCRC = ModbusUtils.CalculateCRC(frame.Slice(0, frame.Length - 2));

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
            ModbusUtils.SwitchEndianness(data);

            return data[0];
        }

        public static void SwitchEndianness<T>(Memory<T> dataset) where T : unmanaged
        {
            ModbusUtils.SwitchEndianness(dataset.Span);
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

                    byte tmp = dataset_bytes[i1];
                    dataset_bytes[i1] = dataset_bytes[i2];
                    dataset_bytes[i2] = tmp;
                }
            }
        }
    }
}
