using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentModbus.ServerMultiUnit;

namespace FluentModbus.Tests
{
    public class MultiModbusRtuServerTests : IClassFixture<XUnitFixture>
    {
        private ITestOutputHelper _logger;

        public MultiModbusRtuServerTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public async void ServerHandlesRequestFire()
        {
            // Arrange
            var serialPort = new FakeSerialPort();

            var server = new MultiUnitRtuServer(new byte[] { 1, 2, 3 }, true);

            server.Start(serialPort);

            var client = new ModbusRtuClient();
            client.Connect(serialPort);

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
}