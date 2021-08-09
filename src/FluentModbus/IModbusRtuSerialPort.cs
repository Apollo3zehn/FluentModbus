using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
    public interface IModbusRtuSerialPort
    {
        #region Methods

        int Read(byte[] buffer, int offset, int count);

        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);

        void Write(byte[] buffer, int offset, int count);

        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token);

        void Open();

        void Close();

        #endregion Methods

        #region Properties

        string PortName { get; }
        bool IsOpen { get; }

        #endregion Properties
    }
}