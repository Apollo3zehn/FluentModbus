using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace FluentModbus
{
    /// <summary>
    /// Provides TCP clients.
    /// </summary>
    public class DefaultTcpClientProvider : ITcpClientProvider
    {
        #region Fields

        private TcpListener _tcpListener;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultTcpClientProvider"/>.
        /// </summary>
        /// <param name="endPoint">The endpoint to listen for new TCP client connections.</param>
        public DefaultTcpClientProvider(IPEndPoint endPoint)
        {
            _tcpListener = new TcpListener(endPoint);
            _tcpListener.Start();
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public Task<TcpClient> AcceptTcpClientAsync()
        {
            return _tcpListener.AcceptTcpClientAsync();
        }

        #endregion

        #region IDisposable Support

        private bool disposedValue;

        /// <summary>
        /// Stops the underlying TCP listener.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _tcpListener.Stop();
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Stops the underlying TCP listener.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
