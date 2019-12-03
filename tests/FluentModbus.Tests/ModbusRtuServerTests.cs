using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluentModbus.Tests
{
    public class ModbusRtuServerTests
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

            var server = new ModbusRtuServer(unitIdentifier: 1);
            server.Start(serialPort);

            var client = new ModbusRtuClient();
            client.Connect(serialPort);

            await Task.Run(() =>
            {
                var data = Enumerable.Range(0, 20).Select(i => (float)i).ToArray();
                var sw = Stopwatch.StartNew();

                for (int i = 0; i < 10000; i++)
                {
                    client.WriteMultipleRegisters(1, 0, data);
                }

                var timePerRequest = sw.Elapsed.TotalMilliseconds / 10000 * 1000;
                _logger.WriteLine($"Time per request: {timePerRequest:F0} us. Frequency: {1 / timePerRequest * 1000 * 1000:F0} requests per second.");

                client.Close();
            });

            server.Stop();

            // Assert
        }
    }
}