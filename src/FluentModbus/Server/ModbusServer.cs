using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("FluentModbus.Tests")]

namespace FluentModbus
{
    /// <summary>
    /// Base class for a Modbus server.
    /// </summary>
    public abstract class ModbusServer : IDisposable
    {
        #region Fields

        private byte _unitIdentifier;

        private int _inputRegisterSize;
        private int _holdingRegisterSize;
        private int _coilSize;
        private int _discreteInputSize;

        #endregion

        #region Constructors

        protected ModbusServer(bool isAsynchronous)
        {
            this.Lock = new object();
            this.IsAsynchronous = isAsynchronous;

            this.MaxInputRegisterAddress = UInt16.MaxValue;
            this.MaxHoldingRegisterAddress = UInt16.MaxValue;
            this.MaxCoilAddress = UInt16.MaxValue;
            this.MaxDiscreteInputAddress = UInt16.MaxValue;

            _inputRegisterSize = (this.MaxInputRegisterAddress + 1) * 2;
            _holdingRegisterSize = (this.MaxHoldingRegisterAddress + 1) * 2;
            _coilSize = (this.MaxCoilAddress + 1 + 7) / 8;
            _discreteInputSize = (this.MaxDiscreteInputAddress + 1 + 7) / 8;

            this.InputRegisterBufferPtr = Marshal.AllocHGlobal(_inputRegisterSize);
            this.HoldingRegisterBufferPtr = Marshal.AllocHGlobal(_holdingRegisterSize);
            this.CoilBufferPtr = Marshal.AllocHGlobal(_coilSize);
            this.DiscreteInputBufferPtr = Marshal.AllocHGlobal(_discreteInputSize);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the unit identifier.
        /// </summary>
        public byte UnitIdentifier
        {
            get
            {
                return _unitIdentifier;
            }
            set
            {
                if (!(1 <= _unitIdentifier && _unitIdentifier <= 247))
                    throw new Exception(ErrorMessage.ModbusServer_InvalidUnitIdentifier);

                _unitIdentifier = value;
            }
        }

        /// <summary>
        /// Gets the lock object. For synchronous operation only.
        /// </summary>
        public object Lock { get; }

        /// <summary>
        /// Gets the operation mode.
        /// </summary>
        public bool IsAsynchronous { get; }

        /// <summary>
        /// Gets the pointer to the input registers buffer.
        /// </summary>
        public IntPtr InputRegisterBufferPtr { get; }

        /// <summary>
        /// Gets the pointer to the holding registers buffer.
        /// </summary>
        public IntPtr HoldingRegisterBufferPtr { get; }

        /// <summary>
        /// Gets the pointer to the coils buffer.
        /// </summary>
        public IntPtr CoilBufferPtr { get; }

        /// <summary>
        /// Gets the pointer to the discete inputs buffer.
        /// </summary>
        public IntPtr DiscreteInputBufferPtr { get; }

        /// <summary>
        /// Gets the maximum input register address.
        /// </summary>
        public UInt16 MaxInputRegisterAddress { get; }

        /// <summary>
        /// Gets the maximum holding register address.
        /// </summary>
        public UInt16 MaxHoldingRegisterAddress { get; }

        /// <summary>
        /// Gets the maximum coil address.
        /// </summary>
        public UInt16 MaxCoilAddress { get; }

        /// <summary>
        /// Gets the maximum discrete input address.
        /// </summary>
        public UInt16 MaxDiscreteInputAddress { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the input register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetInputRegisterBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetInputRegisterBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the input register buffer as byte array.
        /// </summary>
        public unsafe Span<byte> GetInputRegisterBuffer()
        {
            return new Span<byte>(this.InputRegisterBufferPtr.ToPointer(), _inputRegisterSize);
        }

        /// <summary>
        /// Gets the holding register buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetHoldingRegisterBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetHoldingRegisterBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the holding register buffer as byte array.
        /// </summary>
        public unsafe Span<byte> GetHoldingRegisterBuffer()
        {
            return new Span<byte>(this.HoldingRegisterBufferPtr.ToPointer(), _holdingRegisterSize);
        }

        /// <summary>
        /// Gets the coil buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetCoilBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetCoilBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the coil buffer as byte array.
        /// </summary>
        public unsafe Span<byte> GetCoilBuffer()
        {
            return new Span<byte>(this.CoilBufferPtr.ToPointer(), _coilSize);
        }

        /// <summary>
        /// Gets the discrete input buffer as type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the returned array.</typeparam>
        public Span<T> GetDiscreteInputBuffer<T>() where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(this.GetDiscreteInputBuffer());
        }

        /// <summary>
        /// Low level API. Use the generic version for easy access. This method gets the discrete input buffer as byte array.
        /// </summary>
        public unsafe Span<byte> GetDiscreteInputBuffer()
        {
            return new Span<byte>(this.DiscreteInputBufferPtr.ToPointer(), _discreteInputSize);
        }

        /// <summary>
        /// Clears all buffer contents.
        /// </summary>
        public void ClearBuffers()
        {
            this.GetInputRegisterBuffer().Clear();
            this.GetHoldingRegisterBuffer().Clear();
            this.GetCoilBuffer().Clear();
            this.GetDiscreteInputBuffer().Clear();
        }

        /// <summary>
        /// Stops the server operation.
        /// </summary>
        public abstract void Stop();

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.Stop();
                }

                Marshal.FreeHGlobal(this.InputRegisterBufferPtr);
                Marshal.FreeHGlobal(this.HoldingRegisterBufferPtr);
                Marshal.FreeHGlobal(this.CoilBufferPtr);
                Marshal.FreeHGlobal(this.DiscreteInputBufferPtr);

                disposedValue = true;
            }
        }

        ~ModbusServer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes the buffers.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
