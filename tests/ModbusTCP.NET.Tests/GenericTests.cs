using System;
using System.Linq;
using System.Net;
using Xunit;

namespace ModbusTCP.NET.Tests
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