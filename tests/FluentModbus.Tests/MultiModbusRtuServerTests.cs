using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FluentModbus.Tests
{
    public class MultiModbusRtuServerTests : IClassFixture<XUnitFixture>
    {
        private ITestOutputHelper _logger;

        public MultiModbusRtuServerTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        //[Fact]
        //public async void ServerHandlesRequestFire()
        //{
        //    // Arrange
        //    var serialPort = new FakeSerialPort();

        //    var server = new MultiUnitRtuServer(new byte[] { 1, 2, 3 }, true);

        //    server.Start(serialPort);

        //    var client = new ModbusRtuClient();
        //    client.Connect(serialPort);

        //    await Task.Run(() =>
        //    {
        //        var data = Enumerable.Range(0, 20).Select(i => (float)i).ToArray();
        //        var sw = Stopwatch.StartNew();
        //        var iterations = 10000;

        //        for (int i = 0; i < iterations; i++)
        //        {
        //            client.WriteMultipleRegisters(0, 0, data);
        //        }

        //        var timePerRequest = sw.Elapsed.TotalMilliseconds / iterations;
        //        _logger.WriteLine($"Time per request: {timePerRequest * 1000:F0} us. Frequency: {1 / timePerRequest * 1000:F0} requests per second.");

        //        client.Close();
        //    });

        //    // Assert
        //}

        //[Fact]
        //public void UpdateServerRegisters()
        //{
        //    var server = new MultiUnitRtuServer(new byte[] { 1, 2, 3 }, true);

        //    server.Start("COM1");

        //    var registersOne = server.GetHoldingRegisters(1);
        //    var registersTwo = server.GetHoldingRegisters(2);
        //    var registersThree = server.GetHoldingRegisters(3);

        //    registersOne.SetLittleEndian<short>(address: 7, 1);
        //    registersTwo.SetLittleEndian<short>(address: 7, 2);
        //    registersThree.SetLittleEndian<short>(address: 7, 3);

        //    var client = new ModbusRtuClient()
        //    {
        //        BaudRate = server.BaudRate,
        //        Parity = server.Parity,
        //        StopBits = server.StopBits,
        //        Handshake = server.Handshake
        //    };

        //    client.Connect("COM2");

        //    var regValueOne = client.ReadHoldingRegisters<short>(1, 7, 1).ToArray();

        //    var regValueTwo = client.ReadHoldingRegisters<short>(2, 7, 1).ToArray();

        //    var regValueThree = client.ReadHoldingRegisters<short>(3, 7, 1).ToArray();
        //    client.Close();

        //    server.StopListening();
        //    server.Stop();

        //    Assert.Equal(1, regValueOne[0]);

        //    Assert.Equal(2, regValueTwo[0]);

        //    Assert.Equal(3, regValueThree[0]);
        //}

        //[Fact]
        //public void AddDynamicallyUnits()
        //{
        //    var server = new MultiUnitRtuServer(new byte[] { 1 });
        //    server.Start("COM1");
        //    server.AddUnit(2);
        //    server.AddUnit(3);

        //    var registersOne = server.GetHoldingRegisters(1);
        //    var registersTwo = server.GetHoldingRegisters(2);
        //    var registersThree = server.GetHoldingRegisters(3);

        //    registersOne.SetLittleEndian<short>(address: 7, 1);
        //    registersTwo.SetLittleEndian<short>(address: 7, 2);
        //    registersThree.SetLittleEndian<short>(address: 7, 3);

        //    var client = new ModbusRtuClient();
        //    client.Connect("COM2");

        //    var regValueOne = client.ReadHoldingRegisters<short>(1, 7, 1).ToArray();

        //    var regValueTwo = client.ReadHoldingRegisters<short>(2, 7, 1).ToArray();

        //    var regValueThree = client.ReadHoldingRegisters<short>(3, 7, 1).ToArray();
        //    client.Close();

        //    server.StopListening();
        //    server.Stop();

        //    Assert.Equal(1, regValueOne[0]);

        //    Assert.Equal(2, regValueTwo[0]);

        //    Assert.Equal(3, regValueThree[0]);
        //}

        [Fact]
        public void SetMidLittleEndianToInt32()
        {
            var server = new MultiUnitRtuServer(new byte[] { 1 });
            server.Start("COM1");
            server.AddUnit(2);

            var testNumber = 153096;
            var registers = server.GetHoldingRegisters(2);
            registers.SetMidLittleEndian<int>(address: 7, testNumber);

            var client = new ModbusRtuClient();
            client.Connect("COM2");

            var regValueOne = client.ReadHoldingRegisters<int>(2, 7, 1).ToArray();

            var byte_set = BitConverter.GetBytes(regValueOne[0]);
            var convertedValue = BitConverter.ToInt32(new[] { byte_set[1], byte_set[0], byte_set[3], byte_set[2] });

            Assert.Equal(testNumber, convertedValue);

            client.Close();

            server.Stop();
            server.Dispose();
        }

        [Fact]
        public void SetMidLittleEndianToFloat()
        {
            var server = new MultiUnitRtuServer(new byte[] { 1 });
            server.Start("COM1");
            server.AddUnit(2);

            float testNumber = 153096.01f;
            var registers = server.GetHoldingRegisters(2);
            registers.SetMidLittleEndian<float>(address: 7, testNumber);

            var client = new ModbusRtuClient();
            client.Connect("COM2");

            var regValueOne = client.ReadHoldingRegisters<float>(2, 7, 1).ToArray();

            var byte_set = BitConverter.GetBytes(regValueOne[0]);
            var convertedValue = BitConverter.ToSingle(new[] { byte_set[1], byte_set[0], byte_set[3], byte_set[2] });

            Assert.Equal(testNumber, convertedValue);

            client.Close();

            server.Stop();
            server.Dispose();
        }

        [Fact]
        public void SetMidLittleEndianToUint32()
        {
            var server = new MultiUnitRtuServer(new byte[] { 1 });
            server.Start("COM1");
            server.AddUnit(2);

            uint testNumber = (uint)int.MaxValue + 1;
            var registers = server.GetHoldingRegisters(2);
            registers.SetMidLittleEndian<uint>(address: 7, testNumber);

            var client = new ModbusRtuClient();
            client.Connect("COM2");

            var regValueOne = client.ReadHoldingRegisters<uint>(2, 7, 1).ToArray();

            var byte_set = BitConverter.GetBytes(regValueOne[0]);
            var convertedValue = BitConverter.ToUInt32(new[] { byte_set[1], byte_set[0], byte_set[3], byte_set[2] });

            Assert.Equal(testNumber, convertedValue);

            client.Close();

            server.Stop();
            server.Dispose();
        }
    }
}