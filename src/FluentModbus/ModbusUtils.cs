using System;

namespace FluentModbus
{
    internal static class ModbusUtils
    {
#warning Add CRC unit test.
        public static ushort CalculateCRC(Span<byte> buffer)
        {
            ushort crc = 0xFFFF;

            foreach (var value in buffer)
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

        public static bool DetectFrame(byte unitIdentifier, Span<byte> frame)
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

            if (frame.Length < 5)
                return false;

            if (unitIdentifier != 255) // 255 means "skip unit identifier check"
            {
                newUnitIdentifier = frame[0];

                if (newUnitIdentifier != unitIdentifier)
                    return false;
            }

            // CRC check
            var crcBytes = frame.Slice(frame.Length - 2, 2);
            var actualCRC = unchecked((ushort)((crcBytes[1] << 8) + crcBytes[0]));
            var expectedCRC = ModbusUtils.CalculateCRC(frame.Slice(0, frame.Length - 2));

            if (actualCRC != expectedCRC)
                return false;

            return true;
        }
    }
}
