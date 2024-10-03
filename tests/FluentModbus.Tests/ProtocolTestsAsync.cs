using Xunit;

namespace FluentModbus.Tests;

public class ProtocolTestsAsync : IClassFixture<XUnitFixture>
{
    private float[] _array;

    public ProtocolTestsAsync()
    {
        _array = [0, 0, 0, 0, 0, 65.455F, 24, 25, 0, 0];
    }

    // FC03: ReadHoldingRegisters
    [Fact]
    public async Task FC03Test()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();

        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        void AsyncWorkaround()
        {
            var buffer = server.GetHoldingRegisterBuffer<float>();

            buffer[6] = 65.455F;
            buffer[7] = 24;
            buffer[8] = 25;
        }

        lock (server.Lock)
        {
            AsyncWorkaround();
        }

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        // Act
        var actual = await client.ReadHoldingRegistersAsync<float>(0, 2, 10);

        // Assert
        var expected = _array;

        Assert.True(expected.SequenceEqual(actual.ToArray()));
    }

    // FC16: WriteMultipleRegisters
    [Fact]
    public async Task FC16Test()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();

        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        // Act
        await client.WriteMultipleRegistersAsync(0, 2, _array);

        // Assert
        var expected = _array;

        lock (server.Lock)
        {
            var actual = server.GetHoldingRegisterBuffer<float>().Slice(1, 10).ToArray();
            Assert.True(expected.SequenceEqual(actual));
        }
    }

    // FC01: ReadCoils
    [Fact]
    public async Task FC01Test()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();

        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        void AsyncWorkaround()
        {
            var buffer = server.GetCoilBuffer<byte>();

            buffer[1] = 9;
            buffer[2] = 0;
            buffer[3] = 24;
        }

        lock (server.Lock)
        {
            AsyncWorkaround();
        }

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        // Act
        var actual = await client.ReadCoilsAsync(0, 8, 25);

        // Assert
        var expected = new byte[] { 9, 0, 24, 0 };

        Assert.True(expected.SequenceEqual(actual.ToArray()));
    }

    // FC02: ReadDiscreteInputs
    [Fact]
    public async Task FC02Test()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();

        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        void AsyncWorkaround()
        {
            var buffer = server.GetDiscreteInputBuffer<byte>();

            buffer[1] = 9;
            buffer[2] = 0;
            buffer[3] = 24;
        }

        lock (server.Lock)
        {
            AsyncWorkaround();
        }

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        // Act
        var actual = await client.ReadDiscreteInputsAsync(0, 8, 25);

        // Assert
        var expected = new byte[] { 9, 0, 24, 0 };

        Assert.True(expected.SequenceEqual(actual.ToArray()));
    }

    // FC04: ReadInputRegisters
    [Fact]
    public async Task FC04Test()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();

        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        void AsyncWorkaround()
        {
            var buffer = server.GetInputRegisterBuffer<float>();

            buffer[6] = 65.455F;
            buffer[7] = 24;
            buffer[8] = 25;
        }

        lock (server.Lock)
        {
            AsyncWorkaround();
        }

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        // Act
        var actual = await client.ReadInputRegistersAsync<float>(0, 2, 10);

        // Assert
        var expected = _array;

        Assert.True(expected.SequenceEqual(actual.ToArray()));
    }

    // FC05: WriteSingleCoil
    [Fact]
    public async Task FC05Test()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();

        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        // Act
        await client.WriteSingleCoilAsync(0, 2, true);
        await client.WriteSingleCoilAsync(0, 7, true);
        await client.WriteSingleCoilAsync(0, 9, true);
        await client.WriteSingleCoilAsync(0, 26, true);

        // Assert
        var expected = new byte[] { 132, 2, 0, 4 };

        lock (server.Lock)
        {
            var actual = server.GetCoilBuffer<byte>().Slice(0, 4).ToArray();
            Assert.True(expected.SequenceEqual(actual));
        }
    }

    // FC06: WriteSingleRegister
    [Fact]
    public async Task FC06Test()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();

        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        // Act
        await client.WriteSingleRegisterAsync(0, 02, 259);
        await client.WriteSingleRegisterAsync(0, 10, 125);
        await client.WriteSingleRegisterAsync(0, 11, 16544);
        await client.WriteSingleRegisterAsync(0, 12, 4848);

        // Assert
        var expected = new short[] { 0, 0, 259, 0, 0, 0, 0, 0, 0, 0, 125, 16544, 4848 };

        lock (server.Lock)
        {
            var actual = server.GetHoldingRegisterBuffer<short>().Slice(0, 13).ToArray();
            Assert.True(expected.SequenceEqual(actual));
        }
    }

    // F023 ReadWriteMultipleRegisters
    [Fact]
    public async Task FC023Test()
    {
        // Arrange
        var endpoint = EndpointSource.GetNext();

        using var server = new ModbusTcpServer();
        server.Start(endpoint);

        void AsyncWorkaround()
        {
            var buffer = server.GetHoldingRegisterBuffer<float>();

            buffer[6] = 65.455F;
            buffer[7] = 24;
            buffer[8] = 25;
        }

        lock (server.Lock)
        {
            AsyncWorkaround();
        }

        var client = new ModbusTcpClient();
        client.Connect(endpoint);

        // Act
        var actual1 = await client.ReadWriteMultipleRegistersAsync<float, float>(0, 2, 10, 12, new float[] { 1.211F });

        // Assert
        var expected = new float[] { 0, 0, 0, 0, 0, 1.211F, 24, 25, 0, 0 };

        Assert.True(expected.SequenceEqual(actual1.ToArray()));

        lock (server.Lock)
        {
            var actual2 = server.GetHoldingRegisterBuffer<float>().Slice(1, 10).ToArray();
            Assert.True(expected.SequenceEqual(actual2));
        }
    }
}