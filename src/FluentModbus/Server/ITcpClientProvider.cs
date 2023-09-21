using System.Net.Sockets;

namespace FluentModbus
{
    /// <summary>
    /// Provides TCP clients.
    /// </summary>
    public interface ITcpClientProvider : IDisposable
    {
        /// <summary>
        /// Accepts the next TCP client.
        /// </summary>
        /// <returns></returns>
        Task<TcpClient> AcceptTcpClientAsync();

        ushort Port { get; }
    }
}
