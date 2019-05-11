using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FluentModbus
{
    public class ExtendedBinaryReader : BinaryReader
    {
        public ExtendedBinaryReader(Stream stream) : base(stream)
        {
            //
        }

        private T ReadReverse<T>(byte[] data) where T : struct
        {
            data.AsSpan().Reverse();

            return MemoryMarshal.Cast<byte, T>(data)[0];
        }

        public short ReadInt16Reverse()
        {
            return this.ReadReverse<short>(BitConverter.GetBytes(this.ReadInt16()));
        }

        public ushort ReadUInt16Reverse()
        {
            return this.ReadReverse<ushort>(BitConverter.GetBytes(this.ReadUInt16()));
        }

        public int ReadInt32Reverse()
        {
            return this.ReadReverse<int>(BitConverter.GetBytes(this.ReadInt32()));
        }

        public uint ReadUInt32Reverse()
        {
            return this.ReadReverse<uint>(BitConverter.GetBytes(this.ReadUInt32()));
        }

        public long ReadInt64Reverse()
        {
            return this.ReadReverse<long>(BitConverter.GetBytes(this.ReadInt64()));
        }

        public ulong ReadUInt64Reverse()
        {
            return this.ReadReverse<ulong>(BitConverter.GetBytes(this.ReadUInt64()));
        }

        public float ReadFloat32Reverse()
        {
            return this.ReadReverse<float>(BitConverter.GetBytes(this.ReadSingle()));
        }

        public double ReadFloat64Reverse()
        {
            return this.ReadReverse<double>(BitConverter.GetBytes(this.ReadDouble()));
        }
    }
}
