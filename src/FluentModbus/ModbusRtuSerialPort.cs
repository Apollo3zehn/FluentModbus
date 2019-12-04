using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
    internal class ModbusRtuSerialPort : IModbusRtuSerialPort
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

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            return _serialPort.BaseStream.ReadAsync(buffer, offset, count, token);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _serialPort.Write(buffer, offset, count);
        }

        #endregion
    }
}
