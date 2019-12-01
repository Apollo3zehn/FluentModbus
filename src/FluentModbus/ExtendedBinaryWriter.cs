using System;
using System.IO;

namespace FluentModbus
{
    /// <summary>
    /// A binary writer with extended capability to optionally reverse written bytes.
    /// </summary>
    public class ExtendedBinaryWriter : BinaryWriter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExtendedBinaryWriter"/> instance.
        /// </summary>
        /// <param name="stream">The underlying data stream.</param>
        public ExtendedBinaryWriter(Stream stream) : base(stream)
        {
            //
        }

        /// <summary>
        /// Writes the provided byte array to the stream.
        /// </summary>
        /// <param name="data">The data to be written.</param>
        private void WriteReverse(byte[] data)
        {
            Array.Reverse(data);
            base.Write(data);
        }

        /// <summary>
        /// Writes the provided value to the stream.
        /// </summary>
        /// <param name="value">The value to be written.</param>
        public void WriteReverse(short value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        /// <summary>
        /// Writes the provided value to the stream.
        /// </summary>
        /// <param name="value">The value to be written.</param>
        public void WriteReverse(ushort value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        /// <summary>
        /// Writes the provided value to the stream.
        /// </summary>
        /// <param name="value">The value to be written.</param>
        public void WriteReverse(int value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        /// <summary>
        /// Writes the provided value to the stream.
        /// </summary>
        /// <param name="value">The value to be written.</param>
        public void WriteReverse(uint value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        /// <summary>
        /// Writes the provided value to the stream.
        /// </summary>
        /// <param name="value">The value to be written.</param>
        public void WriteReverse(long value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        /// <summary>
        /// Writes the provided value to the stream.
        /// </summary>
        /// <param name="value">The value to be written.</param>
        public void WriteReverse(ulong value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        /// <summary>
        /// Writes the provided value to the stream.
        /// </summary>
        /// <param name="value">The value to be written.</param>
        public void WriteReverse(float value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }

        /// <summary>
        /// Writes the provided value to the stream.
        /// </summary>
        /// <param name="value">The value to be written.</param>
        public void WriteReverse(double value)
        {
            this.WriteReverse(BitConverter.GetBytes(value));
        }
    }
}
