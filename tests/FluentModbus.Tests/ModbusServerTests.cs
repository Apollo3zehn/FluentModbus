using System.Diagnostics;
using System.Runtime.InteropServices;
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

            using var server = new ModbusTcpServer();
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
            _logger.WriteLine($"Time per 1000 read operations: {elapsed.TotalMilliseconds / 1000} ms");
        }

        [Fact]
        public void BigEndianWritePerformanceTest()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            using var server = new ModbusTcpServer();
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
            _logger.WriteLine($"Time per 1000 write operations: {elapsed.TotalMilliseconds / 1000} ms");
        }

        [Fact]
        public async Task BitOperationsTest()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            using var server = new ModbusTcpServer();
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

            using var server = new ModbusTcpServer();
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
                Assert.True(lastRequest2 < lastRequest1);
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

            using var server = new ModbusTcpServer()
            {
                RequestValidator = (unitIdentifier, functionCode, address, quantityOfRegisters) =>
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

            using var server = new ModbusTcpServer();
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

            using var server = new ModbusTcpServer();
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

        [Theory]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(false, false, false)]
        [InlineData(true, true, false)]
        public async Task CanDetectCoilChanged(bool initialValue, bool newValue, bool expected)
        {
            // Arrange
            var actual = false;
            var address = 99;
            var endpoint = EndpointSource.GetNext();

            using var server = new ModbusTcpServer()
            {
                EnableRaisingEvents = true
            };

            server.GetCoils().Set(address, initialValue);

            server.CoilsChanged += (sender, e) =>
            {
                Assert.True(e.Coils.Length == 1);
                actual = e.Coils.Contains(address);
            };

            server.Start(endpoint);

            // Act
            var client = new ModbusTcpClient();

            await Task.Run(() =>
            {
                client.Connect(endpoint);
                client.WriteSingleCoil(0, address, newValue);
            });

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(99, 100, true)]
        [InlineData(0, -1, true)]
        [InlineData(1, 1, false)]
        public async Task CanDetectRegisterChanged(short initialValue, short newValue, bool expected)
        {
            // Arrange
            var actual = false;
            var address = 99;
            var endpoint = EndpointSource.GetNext();

            using var server = new ModbusTcpServer()
            {
                EnableRaisingEvents = true
            };

            server.GetHoldingRegisters()[address] = initialValue;

            server.RegistersChanged += (sender, e) =>
            {
                Assert.True(e.Registers.Length == 1);
                actual = e.Registers.Contains(address);
            };

            server.Start(endpoint);

            // Act
            var client = new ModbusTcpClient();

            await Task.Run(() =>
            {
                client.Connect(endpoint);
                client.WriteSingleRegister(0, address, newValue);
            });

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(false, new short[] { 99, 101, 102 }, new short[] { 100, 101, 103 }, new bool[] { true, false, true })]
        [InlineData(true, new short[] { 99, 101, 102 }, new short[] { 100, 101, 103 }, new bool[] { true, false, true })]
        public async Task CanDetectRegistersChanged(bool useReadWriteMethod, short[] initialValues, short[] newValues, bool[] expected)
        {
            // Arrange
            var actual = new bool[3];
            var address = 99;
            var endpoint = EndpointSource.GetNext();

            using var server = new ModbusTcpServer()
            {
                EnableRaisingEvents = true
            };

            for (int i = 0; i < initialValues.Length; i++)
            {
                server.GetHoldingRegisters()[i + address] = initialValues[i];
            }

            server.RegistersChanged += (sender, e) =>
            {
                Assert.True(e.Registers.Length == 2);

                for (int i = 0; i < initialValues.Length; i++)
                {
                    actual[i] = e.Registers.Contains(address + i);
                }
            };

            server.Start(endpoint);

            // Act
            var client = new ModbusTcpClient();

            await Task.Run(() =>
            {
                client.Connect(endpoint);

                if (useReadWriteMethod)
                    client.ReadWriteMultipleRegisters<short, short>(0, 0, 1, address, newValues);

                else
                    client.WriteMultipleRegisters(0, address, newValues);
            });

            // Assert
            Assert.True(expected.SequenceEqual(actual));
        }

        [Fact]
        public void ServerCanUpdateRegistersPerUnit()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            using var server = new ModbusTcpServer();
            server.AddUnit(1);
            server.AddUnit(2);
            server.AddUnit(3);
            server.Start(endpoint);

            var registersOne = server.GetHoldingRegisters(unitIdentifier: 1);
            var registersTwo = server.GetHoldingRegisters(unitIdentifier: 2);
            var registersThree = server.GetHoldingRegisters(unitIdentifier: 3);

            // Act
            registersOne.SetLittleEndian<short>(address: 7, 1);
            registersTwo.SetLittleEndian<short>(address: 7, 2);
            registersThree.SetLittleEndian<short>(address: 7, 3);

            // Assert
            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            var actualOne = client.ReadHoldingRegisters<short>(unitIdentifier: 1, startingAddress: 7, count: 1).ToArray();
            var actualTwo = client.ReadHoldingRegisters<short>(unitIdentifier: 2, startingAddress: 7, count: 1).ToArray();
            var actualThree = client.ReadHoldingRegisters<short>(unitIdentifier: 3, startingAddress: 7, count: 1).ToArray();

            Assert.Equal(1, actualOne[0]);
            Assert.Equal(2, actualTwo[0]);
            Assert.Equal(3, actualThree[0]);
        }

        [Fact]
        public void ClientCanUpdateRegistersPerUnit()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            using var server = new ModbusTcpServer();
            server.AddUnit(1);
            server.AddUnit(2);
            server.AddUnit(3);
            server.Start(endpoint);

            var registersOne = server.GetHoldingRegisters(unitIdentifier: 1);
            var registersTwo = server.GetHoldingRegisters(unitIdentifier: 2);
            var registersThree = server.GetHoldingRegisters(unitIdentifier: 3);

            // Act
            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            client.WriteMultipleRegisters(unitIdentifier: 1, startingAddress: 7, dataset: new short[] { 1 });
            client.WriteMultipleRegisters(unitIdentifier: 2, startingAddress: 7, dataset: new short[] { 2 });
            client.WriteMultipleRegisters(unitIdentifier: 3, startingAddress: 7, dataset: new short[] { 3 });

            // Assert
            var actualOne = registersOne.GetLittleEndian<short>(address: 7);
            var actualTwo = registersTwo.GetLittleEndian<short>(address: 7);
            var actualThree = registersThree.GetLittleEndian<short>(address: 7);

            Assert.Equal(1, actualOne);
            Assert.Equal(2, actualTwo);
            Assert.Equal(3, actualThree);
        }

        [Fact]
        public void CanAddRemoveUnitsDynamically()
        {
            // Arrange
            const int startingAddress = 7;
            var endpoint = EndpointSource.GetNext();

            using var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            server.AddUnit(1);
            var registersOne = server.GetHoldingRegisters(unitIdentifier: 1);
            registersOne.SetLittleEndian<short>(startingAddress, value: 1);
            var actualOne = client.ReadHoldingRegisters<short>(unitIdentifier: 1, startingAddress, count: 1).ToArray();

            server.AddUnit(2);
            var registersTwo = server.GetHoldingRegisters(unitIdentifier: 2);
            registersTwo.SetLittleEndian<short>(startingAddress, value: 2);
            var actualTwo = client.ReadHoldingRegisters<short>(unitIdentifier: 2, startingAddress, count: 1).ToArray();

            server.AddUnit(3);
            var registersThree = server.GetHoldingRegisters(unitIdentifier: 3);
            registersThree.SetLittleEndian<short>(startingAddress, value: 3);
            var actualThree = client.ReadHoldingRegisters<short>(unitIdentifier: 3, startingAddress, count: 1).ToArray();

            server.RemoveUnit(2);

            // Assert
            Assert.Equal(1, actualOne[0]);
            Assert.Equal(2, actualTwo[0]);
            Assert.Equal(3, actualThree[0]);

            Assert.Throws<KeyNotFoundException>(() => server.GetHoldingRegisters(unitIdentifier: 2));
        }
    }
}