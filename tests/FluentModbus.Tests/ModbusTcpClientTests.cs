using System.Diagnostics;
using Xunit;

namespace FluentModbus.Tests;

public class ModbusTcpClientTests : IClassFixture<XUnitFixture>
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