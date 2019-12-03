using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
    internal interface IModbusRtuSerialPort
    {
        #region Methods

        int Read(byte[] buffer, int offset, int count);
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);
        void Write(byte[] buffer, int offset, int count);
        void Open();
        void Close();

        #endregion

        #region Properties

        bool IsOpen { get; }

        #endregion
    }
}
