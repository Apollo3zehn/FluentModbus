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

        [Theory]

        [InlineData(ModbusFunctionCode.WriteMultipleRegisters, 1, ModbusExceptionCode.IllegalDataAddress)]
        [InlineData(ModbusFunctionCode.WriteMultipleRegisters, 55, ModbusExceptionCode.OK)]
        [InlineData(ModbusFunctionCode.ReadHoldingRegisters, 91, ModbusExceptionCode.IllegalDataAddress)]
        [InlineData(ModbusFunctionCode.ReadHoldingRegisters, 2000, ModbusExceptionCode.OK)]

        [InlineData(ModbusFunctionCode.ReadInputRegisters, 999, ModbusExceptionCode.IllegalDataAddress)]
        [InlineData(ModbusFunctionCode.ReadInputRegisters, 1999, ModbusExceptionCode.OK)]

        [InlineData(ModbusFunctionCode.ReadCoils, 1999, ModbusExceptionCode.IllegalFunction)]
        public async void RespectsRequestValidator(ModbusFunctionCode functionCode, ushort startingAddress, ModbusExceptionCode exceptionCode)
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer()
            {
                RequestValidator = (functionCode, startingAddress, quantityOfRegisters) =>
                {
                    var holdingLimits = (startingAddress >= 50 && startingAddress < 90) ||
                                         startingAddress >= 2000 && startingAddress < 2100;

                    var inputLimits = startingAddress >= 1000 && startingAddress < 2000;

                    return (functionCode, holdingLimits, inputLimits) switch
                    {
                        // holding registers
                        (ModbusFunctionCode.ReadHoldingRegisters, true, _)          => ModbusExceptionCode.OK,
                        (ModbusFunctionCode.ReadWriteMultipleRegisters, true, _)    => ModbusExceptionCode.OK,
                        (ModbusFunctionCode.WriteMultipleRegisters, true, _)        => ModbusExceptionCode.OK,
                        (ModbusFunctionCode.WriteSingleRegister, true, _)           => ModbusExceptionCode.OK,

                        (ModbusFunctionCode.ReadHoldingRegisters, false, _)         => ModbusExceptionCode.IllegalDataAddress,
                        (ModbusFunctionCode.ReadWriteMultipleRegisters, false, _)   => ModbusExceptionCode.IllegalDataAddress,
                        (ModbusFunctionCode.WriteMultipleRegisters, false, _)       => ModbusExceptionCode.IllegalDataAddress,
                        (ModbusFunctionCode.WriteSingleRegister, false, _)          => ModbusExceptionCode.IllegalDataAddress,

                        // input registers
                        (ModbusFunctionCode.ReadInputRegisters, _, true)            => ModbusExceptionCode.OK,
                        (ModbusFunctionCode.ReadInputRegisters, _, false)           => ModbusExceptionCode.IllegalDataAddress,

                        // deny other function codes
                        _                                                           => ModbusExceptionCode.IllegalFunction
                    };
                }
            };

            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            await Task.Run(() =>
            {
                Action action = functionCode switch
                {
                    ModbusFunctionCode.WriteMultipleRegisters   => () => client.WriteMultipleRegisters(0, startingAddress, new double[] { 1.0 }),
                    ModbusFunctionCode.ReadHoldingRegisters     => () => client.ReadHoldingRegisters<double>(0, startingAddress, 1),
                    ModbusFunctionCode.ReadInputRegisters       => () => client.ReadInputRegisters<double>(0, startingAddress, 1),
                    ModbusFunctionCode.ReadCoils                => () => client.ReadCoils(0, startingAddress, 1),
                    _                                           => throw new Exception("Invalid test setup.")
                };

                if (exceptionCode == ModbusExceptionCode.OK)
                {
                    action();
                }
                else
                {
                    var ex = Assert.Throws<ModbusException>(action);

                    // Assert
                    Assert.True(ex.ExceptionCode == exceptionCode);
                }
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