using System;
using System.Linq;
using Xunit;

namespace FluentModbus.Tests
{
    public class ProtocolTests : IClassFixture<XUnitFixture>
    {
        private float[] _array;

        public ProtocolTests()
        {
            _array = new float[] { 0, 0, 0, 0, 0, 65.455F, 24, 25, 0, 0 };
        }

        // FC03: ReadHoldingRegisters
        [Fact]
        public void FC03Test() 
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);
            
            lock (server.Lock)
            {
                var buffer = server.GetHoldingRegisterBuffer<float>();

                buffer[6] = 65.455F;
                buffer[7] = 24;
                buffer[8] = 25;
            }

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            var actual = client.ReadHoldingRegisters<float>(0, 2, 10);

            // Assert
            var expected = _array;

            Assert.True(expected.SequenceEqual(actual.ToArray()));
        }

        // FC16: WriteMultipleRegisters
        [Fact]
        public void FC16Test()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            client.WriteMultipleRegisters(0, 2, _array);

            // Assert
            var expected = _array;

            lock (server.Lock)
            {
                var actual = server.GetHoldingRegisterBuffer<float>().Slice(1, 10);
                Assert.True(expected.SequenceEqual(actual.ToArray()));
            }
        }

        // FC01: ReadCoils
        [Fact]
        public void FC01Test()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            lock (server.Lock)
            {
                var buffer = server.GetCoilBuffer<byte>();

                buffer[1] = 9;
                buffer[2] = 0;
                buffer[3] = 24;
            }

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            var actual = client.ReadCoils(0, 8, 25);

            // Assert
            var expected = new byte[] { 9, 0, 24, 0 };

            Assert.True(expected.SequenceEqual(actual.ToArray()));
        }

        // FC02: ReadDiscreteInputs
        [Fact]
        public void FC02Test()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            lock (server.Lock)
            {
                var buffer = server.GetDiscreteInputBuffer<byte>();

                buffer[1] = 9;
                buffer[2] = 0;
                buffer[3] = 24;
            }

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            var actual = client.ReadDiscreteInputs(0, 8, 25);

            // Assert
            var expected = new byte[] { 9, 0, 24, 0 };

            Assert.True(expected.SequenceEqual(actual.ToArray()));
        }

        // FC04: ReadInputRegisters
        [Fact]
        public void FC04Test()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            lock (server.Lock)
            {
                var buffer = server.GetInputRegisterBuffer<float>();

                buffer[6] = 65.455F;
                buffer[7] = 24;
                buffer[8] = 25;
            }

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            var actual = client.ReadInputRegisters<float>(0, 2, 10);

            // Assert
            var expected = _array;

            Assert.True(expected.SequenceEqual(actual.ToArray()));
        }

        // FC05: WriteSingleCoil
        [Fact]
        public void FC05Test()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            client.WriteSingleCoil(0, 2, true);
            client.WriteSingleCoil(0, 7, true);
            client.WriteSingleCoil(0, 9, true);
            client.WriteSingleCoil(0, 26, true);

            // Assert
            var expected = new byte[] { 132, 2, 0, 4 };

            lock (server.Lock)
            {
                var actual = server.GetCoilBuffer<byte>().Slice(0, 4);
                Assert.True(expected.SequenceEqual(actual.ToArray()));
            }
        }

        // FC06: WriteSingleRegister
        [Fact]
        public void FC06Test()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            client.WriteSingleRegister(0, 02, 259);
            client.WriteSingleRegister(0, 10, 125);
            client.WriteSingleRegister(0, 11, 16544);
            client.WriteSingleRegister(0, 12, 4848);

            // Assert
            var expected = new short[] { 0, 0, 259, 0, 0, 0, 0, 0, 0, 0, 125, 16544, 4848 };

            lock (server.Lock)
            {
                var actual = server.GetHoldingRegisterBuffer<short>().Slice(0, 13);
                Assert.True(expected.SequenceEqual(actual.ToArray()));
            }
        }

        // more tests

        [Fact]
        public void ArraySizeIsCorrectForByteInput()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            var actual = client.ReadHoldingRegisters<byte>(0, 0, 10).ToArray().Count();

            // Assert
            var expected = 10;

            Assert.True(actual == expected);
        }

        [Fact]
        public void ArraySizeIsCorrectForShortInput()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            var actual = client.ReadHoldingRegisters<short>(0, 0, 10).ToArray().Count();

            // Assert
            var expected = 10;

            Assert.True(actual == expected);
        }

        [Fact]
        public void ArraySizeIsCorrectForFloatInput()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            var actual = client.ReadHoldingRegisters<float>(0, 0, 10).ToArray().Count();

            // Assert
            var expected = 10;

            Assert.True(actual == expected);
        }

        [Fact]
        public void ArraySizeIsCorrectForBooleanInput()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            var actual = client.ReadHoldingRegisters<bool>(0, 0, 10).ToArray().Count();

            // Assert
            var expected = 10;

            Assert.True(actual == expected);
        }

        [Fact]
        public void CanReadMaximumNumberOfRegisters()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            client.ReadHoldingRegisters<ushort>(0, 0, 125);
            client.ReadInputRegisters<ushort>(0, 0, 125);
        }

        [Fact]
        public void CanWriteMaximumNumberOfRegisters()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            client.WriteMultipleRegisters<ushort>(0, 0, new ushort[123]);
        }

        [Fact]
        public void ThrowsWhenReadingTooManyRegisters()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            Action action1 = () => client.ReadHoldingRegisters<ushort>(0, 0, 126);
            Action action2 = () => client.ReadInputRegisters<ushort>(0, 0, 126);

            // Assert
            Assert.Throws<ModbusException>(action1);
            Assert.Throws<ModbusException>(action2);
        }

        [Fact]
        public void ThrowsWhenWritingTooManyRegisters()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            Action action = () => client.WriteMultipleRegisters<ushort>(0, 0, new ushort[124]);

            // Assert
            Assert.Throws<ModbusException>(action);
        }

        [Fact]
        public void ThrowsIfZeroBytesAreRequested()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            Action action = () => client.ReadHoldingRegisters<byte>(0, 0, 0);

            // Assert
            Assert.Throws<ModbusException>(action);
        }

        [Fact]
        public void ThrowsIfOddNumberOfBytesIsRequested()
        {
            // Arrange
            var endpoint = EndpointSource.GetNext();

            var server = new ModbusTcpServer();
            server.Start(endpoint);

            var client = new ModbusTcpClient();
            client.Connect(endpoint);

            // Act
            Action action = () => client.ReadHoldingRegisters<byte>(0, 0, 3);

            // Assert

            Assert.Throws<ArgumentOutOfRangeException>(action);
        }
    }
}