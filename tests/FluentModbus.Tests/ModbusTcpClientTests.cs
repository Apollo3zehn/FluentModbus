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

    [Fact]
    public void ClientCanUseBroadcastUnitIdentifier()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();
        
        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        // Act
        client.WriteSingleCoil(unitIdentifier: 0, default, default);
    }

    [Theory]
    [InlineData(247)]
    [InlineData(0xFF)]
    public void ClientCanUseValidUnitIdentifier(byte unitIdentifier)
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();
        
        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        // Act
        client.ReadHoldingRegisters(unitIdentifier, default, default);
    }

    [Theory]
    [InlineData(248)]
    [InlineData(254)]
    public void ClientCannotUseInvalidUnitIdentifier(byte unitIdentifier)
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();
        
        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        void action() => client.ReadHoldingRegisters(unitIdentifier, default, default);

        // Act / Assert
        Assert.Throws<ModbusException>(action);
    }
}