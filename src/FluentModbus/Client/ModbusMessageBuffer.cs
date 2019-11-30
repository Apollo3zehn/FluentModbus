using System;
using System.Buffers;
using System.IO;

namespace FluentModbus
{
    public class ModbusMessageBuffer : IDisposable
    {
        #region Constructors

        public ModbusMessageBuffer(int size)
        {
            this.Buffer = ArrayPool<byte>.Shared.Rent(size);

            this.RequestWriter = new ExtendedBinaryWriter(new MemoryStream(this.Buffer));
            this.ResponseReader = new ExtendedBinaryReader(new MemoryStream(this.Buffer));
        }

        #endregion

        #region Properties

        public byte[] Buffer { get; private set; }

        public ExtendedBinaryWriter RequestWriter { get; private set; }
        public ExtendedBinaryReader ResponseReader { get; private set; }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.RequestWriter.Dispose();
                    this.ResponseReader.Dispose();

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
