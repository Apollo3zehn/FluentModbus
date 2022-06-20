using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
    /// <summary>
    /// A serial port for Modbus RTU communication.
    /// </summary>
    public interface IModbusRtuSerialPort
    {
        #region Methods

        /// <summary>
        /// Reads a number of bytes from the serial port input buffer and writes those bytes into a byte array at the specified offset.
        /// </summary>
        /// <param name="buffer">The byte array to write the input to.</param>
        /// <param name="offset">The offset in <paramref name="buffer"/> at which to write the bytes.</param>
        /// <param name="count">The maximum number of bytes to read. Fewer bytes are read if <paramref name="count"/> is greater than the number of bytes in the input buffer.</param>
        /// <returns>The number of bytes read.</returns>
        int Read(byte[] buffer, int offset, int count);

        /// <summary>
        /// Asynchronously reads a number of bytes from the serial port input buffer and writes those bytes into a byte array at the specified offset.
        /// </summary>
        /// <param name="buffer">The byte array to write the input to.</param>
        /// <param name="offset">The offset in <paramref name="buffer"/> at which to write the bytes.</param>
        /// <param name="count">The maximum number of bytes to read. Fewer bytes are read if <paramref name="count"/> is greater than the number of bytes in the input buffer.</param>
        /// <param name="token">A token to cancel the current operation.</param>
        /// <returns>The number of bytes read.</returns>
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);

        /// <summary>
        /// Writes a specified number of bytes to the serial port using data from a buffer.
        /// </summary>
        /// <param name="buffer">The byte array that contains the data to write to the port.</param>
        /// <param name="offset">The zero-based byte offset in the <paramref name="buffer"/> parameter at which to begin copying bytes to the port.</param>
        /// <param name="count">The number of bytes to write.</param>
        void Write(byte[] buffer, int offset, int count);

        /// <summary>
        /// Asynchronously writes a specified number of bytes to the serial port using data from a buffer.
        /// </summary>
        /// <param name="buffer">The byte array that contains the data to write to the port.</param>
        /// <param name="offset">The zero-based byte offset in the <paramref name="buffer"/> parameter at which to begin copying bytes to the port.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <param name="token">A token to cancel the current operation.</param>
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token);

        /// <summary>
        /// Opens a new serial port connection.
        /// </summary>
        void Open();

        /// <summary>
        /// Closes the port connection, sets the <see cref="IsOpen"/> property to <see langword="true"/>, and disposes of the internal Stream object.
        /// </summary>
        void Close();

        #endregion Methods

        #region Properties

        /// <summary>
        /// Gets the port for communications, including but not limited to all available COM ports.
        /// </summary>
        string PortName { get; }

        /// <summary>
        /// Gets a value indicating the open or closed status of the serial port object.
        /// </summary>
        bool IsOpen { get; }

        #endregion Properties
    }
}