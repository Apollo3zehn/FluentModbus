using System.Runtime.InteropServices;

namespace FluentModbus
{
    /// <summary>
    /// A binary reader with extended capability to handle big-endian data.
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
            return ReadReverse<short>(BitConverter.GetBytes(ReadInt16()));
        }

        /// <summary>
        /// Reads an unsigned short value from the stream.
        /// </summary>
        public ushort ReadUInt16Reverse()
        {
            return ReadReverse<ushort>(BitConverter.GetBytes(ReadUInt16()));
        }

        /// <summary>
        /// Reads a signed integer value from the stream.
        /// </summary>
        public int ReadInt32Reverse()
        {
            return ReadReverse<int>(BitConverter.GetBytes(ReadInt32()));
        }

        /// <summary>
        /// Reads an unsigned integer value from the stream.
        /// </summary>
        public uint ReadUInt32Reverse()
        {
            return ReadReverse<uint>(BitConverter.GetBytes(ReadUInt32()));
        }

        /// <summary>
        /// Reads a signed long value from the stream.
        /// </summary>
        public long ReadInt64Reverse()
        {
            return ReadReverse<long>(BitConverter.GetBytes(ReadInt64()));
        }

        /// <summary>
        /// Reads an unsigned long value from the stream.
        /// </summary>
        public ulong ReadUInt64Reverse()
        {
            return ReadReverse<ulong>(BitConverter.GetBytes(ReadUInt64()));
        }

        /// <summary>
        /// Reads a single value value from the stream.
        /// </summary>
        public float ReadFloat32Reverse()
        {
            return ReadReverse<float>(BitConverter.GetBytes(ReadSingle()));
        }

        /// <summary>
        /// Reads a double value value from the stream.
        /// </summary>
        public double ReadFloat64Reverse()
        {
            return ReadReverse<double>(BitConverter.GetBytes(ReadDouble()));
        }

        private T ReadReverse<T>(byte[] data) where T : struct
        {
            data.AsSpan().Reverse();

            return MemoryMarshal.Cast<byte, T>(data)[0];
        }
    }
}
