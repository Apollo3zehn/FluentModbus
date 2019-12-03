using System;
using System.Diagnostics;
using System.Net;
using Xunit;

namespace FluentModbus.Tests
{
    public class ModbusTcpClientTests
    {
        [Fact]
        public void ClientRespectsConnectTimeout()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();
            var connectTimeout = 500;

            var client = new ModbusTcpClient()
            {
                ConnectTimeout = connectTimeout
            };

            // Act
            var sw = Stopwatch.StartNew();

            try
            {
                client.Connect(endpoint);
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