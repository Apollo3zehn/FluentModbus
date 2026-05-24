using System.Net;

namespace FluentModbus.Tests;

/// <summary>
/// Provides non-routable IP endpoints for tests where the connection needs to hang.
/// Uses the TEST-NET-1 address range (192.0.2.0/24) which is reserved for documentation/testing and guaranteed to be non-routable.
/// See RFC 5737 (https://datatracker.ietf.org/doc/html/rfc5735).
/// </summary>
public static class NonRoutableEndpointSource
{
    private static int _current = 10000;
    private static object _lock = new object();
    private static readonly IPAddress _baseAddress = IPAddress.Parse("192.0.2.1");

    public static IPEndPoint GetNext()
    {
        lock (_lock)
        {
            if (_current == 65535)
            {
                throw new NotSupportedException("There are no more free ports available.");
            }

            return new IPEndPoint(_baseAddress, _current++);
        }
    }
}