using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluentModbus.Tests
{
    public class ModbusServerTests
    {
        private ITestOutputHelper _logger;

        public ModbusServerTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public async void TimeoutIsResetAfterRequest()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            var delay = TimeSpan.FromSeconds(1);

            await Task.Run(async () =>
            {
                var data = Enumerable.Range(0, 20).Select(i => (float)i).ToArray();

                // Act
                await Task.Delay(delay);
                var lastRequest1 = server.RequestHandlerSet.First().LastRequest.Elapsed;
                client.WriteMultipleRegisters(0, 0, data);
                var lastRequest2 = server.RequestHandlerSet.First().LastRequest.Elapsed;

                client.Disconnect();

                // Assert
                Assert.True(lastRequest1 >= delay);
                Assert.True(lastRequest2 < delay);
            });

            server.Stop();
        }
    }
}