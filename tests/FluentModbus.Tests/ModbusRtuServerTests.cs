using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace FluentModbus.Tests;

public class ModbusRtuServerTests : IClassFixture<XUnitFixture>
{
    private ITestOutputHelper _logger;

    public ModbusRtuServerTests(ITestOutputHelper logger)
    {
        _logger = logger;
    }

    [Fact]
    public async void ServerHandlesRequestFire()
    {
        // Arrange
        var serialPort = new FakeSerialPort();

        using var server = new ModbusRtuServer(unitIdentifier: 1);
        server.Start(serialPort);

        var client = new ModbusRtuClient();
        client.Initialize(serialPort, ModbusEndianness.LittleEndian);

        await Task.Run(() =>
        {
            var data = Enumerable.Range(0, 20).Select(i => (float)i).ToArray();
            var sw = Stopwatch.StartNew();
            var iterations = 10000;

            for (int i = 0; i < iterations; i++)
            {
                client.WriteMultipleRegisters(0, 0, data);
            }

            var timePerRequest = sw.Elapsed.TotalMilliseconds / iterations;
            _logger.WriteLine($"Time per request: {timePerRequest * 1000:F0} us. Frequency: {1 / timePerRequest * 1000:F0} requests per second.");

            client.Close();
        });

        // Assert
    }
}