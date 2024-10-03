using System.IO.Ports;

namespace FluentModbus;

/// <summary>
/// A wrapper for a <see cref="SerialPort" />.
/// </summary>
public class ModbusRtuSerialPort : IModbusRtuSerialPort
{
    #region Fields

    private readonly SerialPort _serialPort;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instances of the <see cref="ModbusRtuSerialPort" /> class.
    /// </summary>
    /// <param name="serialPort">The serial port to wrap.</param>
    public ModbusRtuSerialPort(SerialPort serialPort)
    {
        _serialPort = serialPort;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the port for communications.
    /// </summary>
    public string PortName => _serialPort.PortName;

    /// <summary>
    /// Gets a value indicating the open or closed status of the <see cref="ModbusRtuSerialPort" /> object.
    /// </summary>
    public bool IsOpen => _serialPort.IsOpen;

    #endregion

    #region Methods

    /// <summary>
    /// Opens a new serial port connection.
    /// </summary>
    public void Open()
    {
        _serialPort.Open();
    }

    /// <summary>
    /// Closes the port connection, sets the <see cref="IsOpen"/> property to <see langword="false"/>, and disposes of the internal <see cref="Stream"/> object.
    /// </summary>
    public void Close()
    {
        _serialPort.Close();
    }

    /// <summary>
    /// Reads from the <see cref="SerialPort"/> input buffer.
    /// </summary>
    /// <param name="buffer">The byte array to write the input to.</param>
    /// <param name="offset">The offset in <paramref name="buffer"/> at which to write the bytes.</param>
    /// <param name="count">The maximum number of bytes to read. Fewer bytes are read if <paramref name="count"/> is greater than the number of bytes in the input buffer.</param>
    /// <returns>The number of bytes read.</returns>
    public int Read(byte[] buffer, int offset, int count)
    {
        return _serialPort.Read(buffer, offset, count);
    }

    /// <summary>
    /// Asynchronously reads from the <see cref="SerialPort"/> input buffer.
    /// </summary>
    /// <param name="buffer">The byte array to write the input to.</param>
    /// <param name="offset">The offset in <paramref name="buffer"/> at which to write the bytes.</param>
    /// <param name="count">The maximum number of bytes to read. Fewer bytes are read if <paramref name="count"/> is greater than the number of bytes in the input buffer.</param>
    /// <param name="token">A token to cancel the current operation.</param>
    /// <returns>The number of bytes read.</returns>
    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        // https://github.com/AndreasAmMueller/Modbus/blob/a6d11080c2f5a1205681c881f3ba163d2ac84a1f/src/Modbus.Serial/Util/Extensions.cs#L69
        // https://stackoverflow.com/a/54610437/11906695
        // https://github.com/dotnet/runtime/issues/28968

        using var timeoutCts = new CancellationTokenSource(_serialPort.ReadTimeout);

        /* _serialPort.DiscardInBuffer is essential here to cancel the operation */
        using (timeoutCts.Token.Register(() =>
               {
                   if (IsOpen)
                       _serialPort.DiscardInBuffer();
               }))
        using (token.Register(() => timeoutCts.Cancel()))
        {
            try
            {
                return await _serialPort.BaseStream.ReadAsync(buffer, offset, count, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException("The asynchronous read operation timed out.");
            }
            catch (IOException) when (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
            {
                throw new TimeoutException("The asynchronous read operation timed out.");
            }
        }
    }

    /// <summary>
    /// Writes data to the serial port output buffer.
    /// </summary>
    /// <param name="buffer">The byte array that contains the data to write to the port.</param>
    /// <param name="offset">The zero-based byte offset in the <paramref name="buffer"/> parameter at which to begin copying bytes to the port.</param>
    /// <param name="count">The number of bytes to write.</param>
    public void Write(byte[] buffer, int offset, int count)
    {
        _serialPort.Write(buffer, offset, count);
    }

    /// <summary>
    /// Asynchronously writes data to the serial port output buffer.
    /// </summary>
    /// <param name="buffer">The byte array that contains the data to write to the port.</param>
    /// <param name="offset">The zero-based byte offset in the <paramref name="buffer"/> parameter at which to begin copying bytes to the port.</param>
    /// <param name="count">The number of bytes to write.</param>
    /// <param name="token">A token to cancel the current operation.</param>
    public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        // https://github.com/AndreasAmMueller/Modbus/blob/a6d11080c2f5a1205681c881f3ba163d2ac84a1f/src/Modbus.Serial/Util/Extensions.cs#L69
        // https://stackoverflow.com/a/54610437/11906695
        // https://github.com/dotnet/runtime/issues/28968
        using var timeoutCts = new CancellationTokenSource(_serialPort.WriteTimeout);

        /* _serialPort.DiscardInBuffer is essential here to cancel the operation */
        using (timeoutCts.Token.Register(() => _serialPort.DiscardOutBuffer()))
        using (token.Register(() => timeoutCts.Cancel()))
        {
            try
            {
                await _serialPort.BaseStream.WriteAsync(buffer, offset, count, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException("The asynchronous write operation timed out.");
            }
            catch (IOException) when (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
            {
                throw new TimeoutException("The asynchronous write operation timed out.");
            }
        }
    }

    #endregion
}
