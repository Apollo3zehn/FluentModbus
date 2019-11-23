using System;
using System.Linq;
using System.Net;
using Xunit;

namespace FluentModbus.Tests
{
    public class GenericTests
    {
        private static ModbusTcpServer _server;
        private IPEndPoint _endpoint;

        static GenericTests()
        {
            _server = new ModbusTcpServer();
        }

        public GenericTests()
        {
            _endpoint = new IPEndPoint(IPAddress.Loopback, 20001);
        }

        [Fact]
        public void ArraySizeIsCorrectForByteInput() 
        {
            // Arrange
            _server.Start(_endpoint);
            
            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

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
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

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
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

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
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

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
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

            // Act
            client.ReadHoldingRegisters<ushort>(0, 0, 125);
            client.ReadInputRegisters<ushort>(0, 0, 125);
        }

        [Fact]
        public void CanWriteMaximumNumberOfRegisters()
        {
            // Arrange
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

            // Act
            client.WriteMultipleRegisters<ushort>(0, 0, new ushort[123]);
        }

        [Fact]
        public void ThrowsWhenReadingTooManyRegisters()
        {
            // Arrange
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

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
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

            // Act
            Action action = () => client.WriteMultipleRegisters<ushort>(0, 0, new ushort[124]);

            // Assert
            Assert.Throws<ModbusException>(action);
        }

        [Fact]
        public void ThrowsIfZeroBytesAreRequested()
        {
            // Arrange
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

            // Act
            Action action = () => client.ReadHoldingRegisters<byte>(0, 0, 0);

            // Assert
            Assert.Throws<ModbusException>(action);
        }

        [Fact]
        public void ThrowsIfOddNumberOfBytesIsRequested()
        {
            // Arrange
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

            // Act
            Action action = () => client.ReadHoldingRegisters<byte>(0, 0, 3);

            // Assert

            Assert.Throws<ArgumentOutOfRangeException>(action);
        }
    }
}