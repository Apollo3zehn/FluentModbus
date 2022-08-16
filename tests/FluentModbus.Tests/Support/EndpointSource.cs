using System.Net;

namespace FluentModbus.Tests
{
    public static class EndpointSource
    {
        private static int _current = 10000;
        private static object _lock = new object();

        public static IPEndPoint GetNext()
        {
            lock(_lock)
            {
                if (_current == 65535)
                {
                    throw new NotSupportedException("There are no more free ports available.");
                }

                return new IPEndPoint(IPAddress.Loopback, _current++);
            }
        }
    }
}