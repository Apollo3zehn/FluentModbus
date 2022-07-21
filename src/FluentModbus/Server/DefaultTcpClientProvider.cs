using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace FluentModbus
{
    internal class DefaultTcpClientProvider : ITcpClientProvider
    {
        #region Fields

        private TcpListener _tcpListener;

        #endregion

        #region Constructors

        public DefaultTcpClientProvider(IPEndPoint endPoint)
        {
            _tcpListener = new TcpListener(endPoint);
            _tcpListener.Start();
        }

        #endregion

        #region Methods

        public Task<TcpClient> AcceptTcpClientAsync()
        {
            return _tcpListener.AcceptTcpClientAsync();
        }

        #endregion

        #region IDisposable Support

        private bool _disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _tcpListener.Stop();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
