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
    }
}
