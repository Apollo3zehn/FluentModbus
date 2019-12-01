using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FluentModbus
{
    /// <summary>
    /// A binary reader with extended capability to optionally reverse read bytes.
    /// </summary>
    public class ExtendedBinaryReader : BinaryReader
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExtendedBinaryReader"/> instance.
        /// </summary>
        /// <param name="stream">The underlying data stream.</param>
        public ExtendedBinaryReader(Stream stream) : base(stream)
        {
            //
        }

        /// <summary>
        /// Reads a signed short value from the stream.
        /// </summary>
        public short ReadInt16Reverse()
        {
            return this.ReadReverse<short>(BitConverter.GetBytes(this.ReadInt16()));
        }

        /// <summary>
        /// Reads an unsigned short value from the stream.
        /// </summary>
        public ushort ReadUInt16Reverse()
        {
            return this.ReadReverse<ushort>(BitConverter.GetBytes(this.ReadUInt16()));
        }

        /// <summary>
        /// Reads a signed integer value from the stream.
        /// </summary>
        public int ReadInt32Reverse()
        {
            return this.ReadReverse<int>(BitConverter.GetBytes(this.ReadInt32()));
        }

        /// <summary>
        /// Reads an unsigned integer value from the stream.
        /// </summary>
        public uint ReadUInt32Reverse()
        {
            return this.ReadReverse<uint>(BitConverter.GetBytes(this.ReadUInt32()));
        }

        /// <summary>
        /// Reads a signed long value from the stream.
        /// </summary>
        public long ReadInt64Reverse()
        {
            return this.ReadReverse<long>(BitConverter.GetBytes(this.ReadInt64()));
        }

        /// <summary>
        /// Reads an unsigned long value from the stream.
        /// </summary>
        public ulong ReadUInt64Reverse()
        {
            return this.ReadReverse<ulong>(BitConverter.GetBytes(this.ReadUInt64()));
        }

        /// <summary>
        /// Reads a single value value from the stream.
        /// </summary>
        public float ReadFloat32Reverse()
        {
            return this.ReadReverse<float>(BitConverter.GetBytes(this.ReadSingle()));
        }

        /// <summary>
        /// Reads a double value value from the stream.
        /// </summary>
        public double ReadFloat64Reverse()
        {
            return this.ReadReverse<double>(BitConverter.GetBytes(this.ReadDouble()));
        }

        private T ReadReverse<T>(byte[] data) where T : struct
        {
            data.AsSpan().Reverse();

            return MemoryMarshal.Cast<byte, T>(data)[0];
        }
    }
}
