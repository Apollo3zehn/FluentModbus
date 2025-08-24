using System.Diagnostics;
using Xunit;

namespace FluentModbus.Tests;

public class ModbusRtuOverTcpClientTests : IClassFixture<XUnitFixture>
{
    [Fact]
    public void ClientRespectsConnectTimeout()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();
        var connectTimeout = 500;

        var client = new ModbusRtuOverTcpClient()
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

    [Fact]
    public void ClientCanConnectSuccessfully()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();
        var server = new ModbusTcpServer();
        server.Start(endpoint);

        var client = new ModbusRtuOverTcpClient();

        try
        {
            // Act
            client.Connect(endpoint);

            // Assert
            Assert.True(client.IsConnected, "The client should be connected after successful connection.");
        }
        finally
        {
            // Cleanup
            client.Disconnect();
            server.Stop();
        }
    }

    [Fact]
    public async Task ClientRespectsConnectAsyncTimeout()
    {
        // Arrange - use a non-routable IP address to ensure the connection hangs
        var endpoint = NonRoutableEndpointSource.GetNext();
        var connectTimeout = 500;

        var client = new ModbusRtuOverTcpClient()
        {
            ConnectTimeout = connectTimeout
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(async () =>
        {
            await client.ConnectAsync(endpoint);
        });
        Assert.Equal(ErrorMessage.ModbusClient_TcpConnectTimeout, ex.Message);
    }

    [Fact]
    public async Task ClientRespectsConnectAsyncCancellationToken()
    {
        // Arrange - use a non-routable IP address to ensure the connection hangs
        var endpoint = NonRoutableEndpointSource.GetNext();
        var client = new ModbusRtuOverTcpClient()
        {
            ConnectTimeout = 5000 // Set longer timeout so cancellation token wins
        };
        var cts = new CancellationTokenSource(500);

        // Act & Assert
        var sw = Stopwatch.StartNew();
        
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await client.ConnectAsync(endpoint, cts.Token);
        });

        var elapsed = sw.ElapsedMilliseconds;
        Assert.True(elapsed < 1000, "The cancellation token was not respected.");
    }

    [Fact]
    public async Task ClientCanConnectAsyncSuccessfully()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();
        var server = new ModbusTcpServer();
        server.Start(endpoint);

        var client = new ModbusRtuOverTcpClient();

        try
        {
            // Act
            await client.ConnectAsync(endpoint);

            // Assert
            Assert.True(client.IsConnected, "The client should be connected after successful async connection.");
        }
        finally
        {
            // Cleanup
            client.Disconnect();
            server.Stop();
        }
    }
}