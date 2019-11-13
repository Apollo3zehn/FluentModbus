using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluentModbus.Tests
{
    public class ServerTests
    {
        private ITestOutputHelper _logger;
        private IPEndPoint _endpoint;

        public ServerTests(ITestOutputHelper logger)
        {
            _logger = logger;
            _endpoint = new IPEndPoint(IPAddress.Loopback, 20002);
        }

        [Fact(Skip = "Test depends on machine power.")]
        public async void ServerHandlesMultipleClients()
        {
            // Arrange
            var server = new ModbusTcpServer();
            server.Start(_endpoint);

            // Act
            var clients = Enumerable.Range(0, 20).Select(index => new ModbusTcpClient()).ToList();

            var tasks = clients.Select((client, index) =>
            {
                var data = Enumerable.Range(0, 20).Select(i => (float)i).ToArray();

                client.Connect(_endpoint);
                _logger.WriteLine($"Client {index}: Connected.");

                return Task.Run(async () =>
                {
                    _logger.WriteLine($"Client {index}: Task started.");

                    for (int i = 0; i < 10; i++)
                    {
                        client.ReadHoldingRegisters<short>(0, 0, 100);
                        _logger.WriteLine($"Client {index}: ReadHoldingRegisters({i})");
                        await Task.Delay(50);
                        client.WriteMultipleRegisters(0, 0, data);
                        _logger.WriteLine($"Client {index}: WriteMultipleRegisters({i})");
                        await Task.Delay(50);
                        client.ReadCoils(0, 0, 25);
                        _logger.WriteLine($"Client {index}: ReadCoils({i})");
                        await Task.Delay(50);
                        client.ReadInputRegisters<float>(0, 0, 20);
                        _logger.WriteLine($"Client {index}: ReadInputRegisters({i})");
                        await Task.Delay(50);
                    }

                    client.Disconnect();
                });
            }).ToList();

            await Task.WhenAll(tasks);

            server.Stop();

            // Assert
        }

        [Fact]
        public async void ServerHandlesRequestFire()
        {
            // Arrange
            var server = new ModbusTcpServer();
            server.Start(_endpoint);

            // Act
            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

            await Task.Run(() =>
            {
                var data = Enumerable.Range(0, 20).Select(i => (float)i).ToArray();
                var sw = Stopwatch.StartNew();

                for (int i = 0; i < 10000; i++)
                {
                    client.WriteMultipleRegisters(0, 0, data);
                }

                var timePerRequest = sw.Elapsed.TotalMilliseconds / 10000 * 1000;
                _logger.WriteLine($"Time per request: {timePerRequest:F0} us. Frequency: {1/timePerRequest * 1000 * 1000:F0} requests per second.");

                client.Disconnect();
            });

            server.Stop();

            // Assert
        }
    }
}