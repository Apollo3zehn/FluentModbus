using System.Linq;
using System.Net;
using Xunit;

namespace FluentModbus.Tests
{
    public class ProtocolTests
    {
        private static ModbusTcpServer _server;
        private IPEndPoint _endpoint;

        private float[] _array;

        static ProtocolTests()
        {
            _server = new ModbusTcpServer();
        }

        public ProtocolTests()
        {
            _array = new float[] { 0, 0, 0, 0, 0, 65.455F, 24, 25, 0, 0 };
            _endpoint = new IPEndPoint(IPAddress.Loopback, 20000);
        }

        // FC03: ReadHoldingRegisters
        [Fact]
        public void FC03Test() 
        {
            // Arrange
            _server.Start(_endpoint);
            
            lock (_server.Lock)
            {
                var buffer = _server.GetHoldingRegisterBuffer<float>();

                buffer[6] = 65.455F;
                buffer[7] = 24;
                buffer[8] = 25;
            }

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

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
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

            // Act
            client.WriteMultipleRegisters(0, 2, _array);

            // Assert
            var expected = _array;

            lock (_server.Lock)
            {
                var actual = _server.GetHoldingRegisterBuffer<float>().Slice(1, 10);
                Assert.True(expected.SequenceEqual(actual.ToArray()));
            }
        }

        // FC01: ReadCoils
        [Fact]
        public void FC01Test()
        {
            // Arrange
            _server.Start(_endpoint);

            lock (_server.Lock)
            {
                var buffer = _server.GetCoilBuffer<byte>();

                buffer[1] = 9;
                buffer[2] = 0;
                buffer[3] = 24;
            }

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

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
            _server.Start(_endpoint);

            lock (_server.Lock)
            {
                var buffer = _server.GetDiscreteInputBuffer<byte>();

                buffer[1] = 9;
                buffer[2] = 0;
                buffer[3] = 24;
            }

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

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
            _server.Start(_endpoint);         

            lock (_server.Lock)
            {
                var buffer = _server.GetInputRegisterBuffer<float>();

                buffer[6] = 65.455F;
                buffer[7] = 24;
                buffer[8] = 25;
            }

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

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
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

            // Act
            client.WriteSingleCoil(0, 2, true);
            client.WriteSingleCoil(0, 7, true);
            client.WriteSingleCoil(0, 9, true);
            client.WriteSingleCoil(0, 26, true);

            // Assert
            var expected = new byte[] { 132, 2, 0, 4 };

            lock (_server.Lock)
            {
                var actual = _server.GetCoilBuffer<byte>().Slice(0, 4);
                Assert.True(expected.SequenceEqual(actual.ToArray()));
            }
        }

        // FC06: WriteSingleRegister
        [Fact]
        public void FC06Test()
        {
            // Arrange
            _server.Start(_endpoint);

            var client = new ModbusTcpClient();
            client.Connect(_endpoint);

            // Act
            client.WriteSingleRegister(0, 02, 259);
            client.WriteSingleRegister(0, 10, 125);
            client.WriteSingleRegister(0, 11, 16544);
            client.WriteSingleRegister(0, 12, 4848);

            // Assert
            var expected = new short[] { 0, 0, 259, 0, 0, 0, 0, 0, 0, 0, 125, 16544, 4848 };

            lock (_server.Lock)
            {
                var actual = _server.GetHoldingRegisterBuffer<short>().Slice(0, 13);
                Assert.True(expected.SequenceEqual(actual.ToArray()));
            }
        }
    }
}