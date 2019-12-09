using System;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
    public class FakeSerialPort : IModbusRtuSerialPort
    {
        #region Fields

        private int _length;
        private byte[] _buffer;
        private AutoResetEvent _autoResetEvent;

        #endregion

        #region Constructors

        public FakeSerialPort()
        {
            _buffer = new byte[260];
            _autoResetEvent = new AutoResetEvent(false);
        }

        #endregion

        #region Properties

        public string PortName => "fake port";

        public bool IsOpen { get; set; }

        #endregion

        #region Methods

        public void Open()
        {
            this.IsOpen = true;
        }

        public void Close()
        {
            this.IsOpen = false;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            _autoResetEvent.WaitOne();
            Buffer.BlockCopy(_buffer, 0, buffer, offset, count);

            return _length;
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            if (!this.IsOpen)
                throw new Exception("This method is only available when the port is open.");

            var registration = token.Register(() =>
            {
                _length = 0;
                _autoResetEvent.Set();
            });

            await Task.Run(() => this.Read(buffer, offset, count), token);

            registration.Dispose();

            if (_length == 0)
                throw new TaskCanceledException();

            return _length;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            Buffer.BlockCopy(buffer, offset, _buffer, 0, count);

            _length = count;
            _autoResetEvent.Set();
        }

        #endregion
    }
}
