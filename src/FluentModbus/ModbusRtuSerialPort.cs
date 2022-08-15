using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
    public class ModbusRtuSerialPort : IModbusRtuSerialPort
    {
        #region Fields

        SerialPort _serialPort;

        #endregion

        #region Constructors

        public ModbusRtuSerialPort(SerialPort serialPort)
        {
            _serialPort = serialPort;
        }

        #endregion

        #region Properties

        public string PortName => _serialPort.PortName;

        public bool IsOpen => _serialPort.IsOpen;

        #endregion

        #region Methods

        public void Open()
        {
            _serialPort.Open();
        }

        public void Close()
        {
            _serialPort.Close();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return _serialPort.Read(buffer, offset, count);
        }

        // https://github.com/AndreasAmMueller/Modbus/blob/a6d11080c2f5a1205681c881f3ba163d2ac84a1f/src/Modbus.Serial/Util/Extensions.cs#L69
        // https://stackoverflow.com/a/54610437/11906695
        // https://github.com/dotnet/runtime/issues/28968
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            using var timeoutCts = new CancellationTokenSource(_serialPort.ReadTimeout);

            /* _serialPort.DiscardInBuffer is essential here to cancel the operation */
            using (timeoutCts.Token.Register(() => _serialPort.DiscardInBuffer()))
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

        public void Write(byte[] buffer, int offset, int count)
        {
            _serialPort.Write(buffer, offset, count);
        }

        // https://github.com/AndreasAmMueller/Modbus/blob/a6d11080c2f5a1205681c881f3ba163d2ac84a1f/src/Modbus.Serial/Util/Extensions.cs#L69
        // https://stackoverflow.com/a/54610437/11906695
        // https://github.com/dotnet/runtime/issues/28968
        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
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
}
