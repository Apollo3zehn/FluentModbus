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
            Buffer = ArrayPool<byte>.Shared.Rent(size);

            Writer = new ExtendedBinaryWriter(new MemoryStream(Buffer));
            Reader = new ExtendedBinaryReader(new MemoryStream(Buffer));
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
                    Writer.Dispose();
                    Reader.Dispose();

                    ArrayPool<byte>.Shared.Return(Buffer);
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