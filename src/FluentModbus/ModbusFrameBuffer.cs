using System;
using System.Buffers;
using System.IO;

namespace FluentModbus
{
    internal class ModbusFrameBuffer : IDisposable
    {
        #region Constructors

        public ModbusFrameBuffer(int size)
        {
            this.Buffer = ArrayPool<byte>.Shared.Rent(size);

            this.Writer = new ExtendedBinaryWriter(new MemoryStream(this.Buffer));
            this.Reader = new ExtendedBinaryReader(new MemoryStream(this.Buffer));
        }

        #endregion

        #region Properties

        public byte[] Buffer { get; }

        public ExtendedBinaryWriter Writer { get; }
        public ExtendedBinaryReader Reader { get; }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.Writer.Dispose();
                    this.Reader.Dispose();

                    ArrayPool<byte>.Shared.Return(this.Buffer);
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}