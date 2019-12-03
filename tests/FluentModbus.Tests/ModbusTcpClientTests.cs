using System;
using System.Diagnostics;
using System.Net;
using Xunit;

namespace FluentModbus.Tests
{
    public class ModbusTcpClientTests
    {
        private IPEndPoint _endpoint;

        public ModbusTcpClientTests()
        {
            _endpoint = new IPEndPoint(IPAddress.Loopback, 20001);
        }

        [Fact]
        public void ClientRespectsConnectTimeout()
        {
            // Arrange
            var connectTimeout = 500;

            var client = new ModbusTcpClient()
            {
                ConnectTimeout = connectTimeout
            };

            // Act
            var sw = Stopwatch.StartNew();

            try
            {
                client.Connect(_endpoint);
            }
            catch (Exception)
            {
                // Assert
                var elapsed = sw.ElapsedMilliseconds;

                Assert.True(elapsed < connectTimeout * 2, "The connect timeout is not respected.");
            }
        }
    }
}