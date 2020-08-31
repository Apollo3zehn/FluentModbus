using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluentModbus.Tests
{
    public class ModbusServerTests : IClassFixture<XUnitFixture>
    {
        private ITestOutputHelper _logger;

        public ModbusServerTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public void BigEndianReadPerformanceTest()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);            

            var buffer = server.GetHoldingRegisters();
            buffer.SetValueBigEndian(startingAddress: 1, 12334234.0);

            // Act
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < 1_000_000; i++)
            {
                var value = buffer.GetValueBigEndian<double>(startingAddress: 1);
            }

            // Assert
            var elapsed = sw.Elapsed;
            server.Stop();

            _logger.WriteLine($"Time per 1000 read operations: {elapsed.TotalMilliseconds / 1000} ms");
        }

        [Fact]
        public void BigEndianWritePerformanceTest()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);            

            var buffer = server.GetHoldingRegisters();

            // Act
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < 1_000_000; i++)
            {
                buffer.SetValueBigEndian(startingAddress: 1, 12334234.0);
            }

            // Assert
            var elapsed = sw.Elapsed;
            server.Stop();

            _logger.WriteLine($"Time per 1000 write operations: {elapsed.TotalMilliseconds / 1000} ms");
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
                var lastRequest1 = server.RequestHandlers.First().LastRequest.Elapsed;
                client.WriteMultipleRegisters(0, 0, data);
                var lastRequest2 = server.RequestHandlers.First().LastRequest.Elapsed;

                client.Disconnect();

                // Assert
                Assert.True(lastRequest1 >= delay);
                Assert.True(lastRequest2 < delay);
            });

            server.Stop();
        }

        [Fact]
        public void CanSetAndGetValueBigEndianHoldingRegisters()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var expected = 12334234.997e-2;

            var byteExpected = MemoryMarshal
                .AsBytes(new double[] { expected }
                .AsSpan());

            byteExpected.Reverse();

            // Act
            var registers = server.GetHoldingRegisters();

            registers.SetValueBigEndian(startingAddress: 2, expected);
            var actual = registers.GetValueBigEndian<double>(startingAddress: 2);

            var byteActual = server
                .GetHoldingRegisterBuffer()
                .Slice(2, 8)
                .ToArray();

            // Assert
            Assert.Equal(expected, actual);
            Assert.True(byteExpected.SequenceEqual(byteActual));
        }

        [Fact]
        public void CanSetAndGetValueBigEndianInputRegisters()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var expected = 12334234.997e-2;

            var byteExpected = MemoryMarshal
                .AsBytes(new double[] { expected }
                .AsSpan());

            byteExpected.Reverse();

            // Act
            var registers = server.GetInputRegisters();

            registers.SetValueBigEndian(startingAddress: 2, expected);
            var actual = registers.GetValueBigEndian<double>(startingAddress: 2);

            var byteActual = server
                .GetInputRegisterBuffer()
                .Slice(2, 8)
                .ToArray();

            // Assert
            Assert.Equal(expected, actual);
            Assert.True(byteExpected.SequenceEqual(byteActual));
        }
    }
}