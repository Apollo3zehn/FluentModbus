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
            buffer.SetBigEndian(address: 1, 12334234.0);

            // Act
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < 1_000_000; i++)
            {
                var value = buffer.GetBigEndian<double>(address: 1);
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
                buffer.SetBigEndian(address: 1, 12334234.0);
            }

            // Assert
            var elapsed = sw.Elapsed;
            server.Stop();

            _logger.WriteLine($"Time per 1000 write operations: {elapsed.TotalMilliseconds / 1000} ms");
        }

        [Fact]
        public async Task BitOperationsTest()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            var expectedArray = new byte[] { 0x34, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x03 };

            await Task.Run(() =>
            {
                var buffer = server.GetCoils();

                // Act + Assert
                buffer.Set(8554, true);
                buffer.Set(8556, true);
                buffer.Set(8557, true);

                var actual1 = buffer.Get(8554);
                var actual2 = buffer.Get(8555);
                var actual3 = buffer.Get(8556);
                var actual4 = buffer.Get(8557);

                Assert.True(actual1);
                Assert.False(actual2);
                Assert.True(actual3);
                Assert.True(actual4);

                buffer.Set(8557, false);
                var actual5 = buffer.Get(8557);
                Assert.False(actual5);

                buffer.Toggle(8557);
                var actual6 = buffer.Get(8557);
                Assert.True(actual6);

                buffer.Set(8607, true);
                buffer.Set(8608, true);
                var actual7 = buffer.Get(8607);
                var actual8 = buffer.Get(8608);

                Assert.True(actual7);
                Assert.True(actual8);

                buffer.Toggle(8609);
                var actual9 = buffer.Get(8609);

                Assert.True(actual9);

                var actualArray = buffer[1069..1077];
                Assert.True(actualArray.SequenceEqual(expectedArray));
            });

            server.Stop();
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
                RequestValidator = (functionCode, address, quantityOfRegisters) =>
                {
                    var holdingLimits = (address >= 50 && address < 90) ||
                                         address >= 2000 && address < 2100;

                    var inputLimits = address >= 1000 && address < 2000;

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
        public void CanSetAndGetBigEndianHoldingRegisters()
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

            registers.SetBigEndian(address: 2, expected);
            var actual = registers.GetBigEndian<double>(address: 2);

            var byteActual = server
                .GetHoldingRegisterBuffer()
                .Slice(2 * 2, 8)
                .ToArray();

            // Assert
            Assert.Equal(expected, actual);
            Assert.True(byteExpected.SequenceEqual(byteActual));
        }

        [Fact]
        public void CanSetAndGetBigEndianInputRegisters()
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

            registers.SetBigEndian(address: 2, expected);
            var actual = registers.GetBigEndian<double>(address: 2);

            var byteActual = server
                .GetInputRegisterBuffer()
                .Slice(2 * 2, 8)
                .ToArray();

            // Assert
            Assert.Equal(expected, actual);
            Assert.True(byteExpected.SequenceEqual(byteActual));
        }
    }
}