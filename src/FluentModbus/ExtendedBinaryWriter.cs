using System;
using System.IO;

namespace FluentModbus
{
    public class ExtendedBinaryWriter : BinaryWriter
    {
        public ExtendedBinaryWriter(Stream stream) : base(stream)
        {
            //
        }

        private void WriteReverse(byte[] data)
        {
            Array.Reverse(data);
            base.Write(data);
        }

        public void WriteReverse(short value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        public void WriteReverse(ushort value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        public void WriteReverse(int value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        public void WriteReverse(uint value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        public void WriteReverse(long value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        public void WriteReverse(ulong value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        public void WriteReverse(float value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        public void WriteReverse(double value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }
    }
}
