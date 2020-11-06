using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluentModbus.Tests
{
    public class ModbusTcpServerTests : IClassFixture<XUnitFixture>
    {
        private ITestOutputHelper _logger;

        public ModbusTcpServerTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public async void ServerWorksWithExternalTcpClient()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();
            var listener = new TcpListener(endpoint);
            var server = new ModbusTcpServer();
            var client = new ModbusTcpClient();

            var expected = new double[] { 0.1, 0.2, 0.3, 0.4, 0.5 };
            double[] actual = null;

            // Act
            listener.Start();

            var clientTask = Task.Run(() =>
            {
                client.Connect(endpoint);

                actual = client
                    .ReadWriteMultipleRegisters<double, double>(0, 0, 5, 0, expected)
                    .ToArray();

                client.Disconnect();
            });

            var serverTask = Task.Run(() =>
            {
                var tcpClient = listener.AcceptTcpClient();
                server.Start(tcpClient);
            });

            await clientTask;
            server.Stop();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact(Skip = "Test depends on machine power.")]
        public async void ServerHandlesMultipleClients()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            // Act
            var clients = Enumerable.Range(0, 20).Select(index => new ModbusTcpClient()).ToList();

            var tasks = clients.Select((client, index) =>
            {
                var data = Enumerable.Range(0, 20).Select(i => (float)i).ToArray();

                client.Connect(endpoint);
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
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            // Act
            var client = new ModbusTcpClient();
            client.Connect(endpoint);

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
                _logger.WriteLine($"Time per request: {timePerRequest * 1000:F0} us. Frequency: {1/timePerRequest * 1000:F0} requests per second.");

                client.Disconnect();
            });

            server.Stop();

            // Assert
        }

        [Fact]
        public async void ServerRespectsMaxClientConnectionLimit()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer()
            {
                MaxConnections = 2
            };

            server.Start(endpoint);

            // Act
            var client1 = new ModbusTcpClient();
            var client2 = new ModbusTcpClient();
            var client3 = new ModbusTcpClient();

            await Task.Run(() =>
            {
                client1.Connect(endpoint);
                client1.WriteSingleRegister(1, 2, 3);

                client2.Connect(endpoint);
                client2.WriteSingleRegister(1, 2, 3);

                client3.Connect(endpoint);

                try
                {
                    client3.WriteSingleRegister(1, 2, 3);
                }

                // Windows
                catch (IOException) { }

                // Linux
                catch (InvalidOperationException) { }

                server.MaxConnections = 3;

                client3.Connect(endpoint);
                client3.WriteSingleRegister(1, 2, 3);
            });

            server.Stop();

            // Assert
        }
    }
}