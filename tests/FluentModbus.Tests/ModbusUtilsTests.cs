using System;
using System.Collections.Generic;
using System.Net;
using Xunit;

namespace FluentModbus.Tests
{
    public class ModbusUtilsTests : IClassFixture<XUnitFixture>
    {
        [Theory]
        [InlineData("127.0.0.1:502", "127.0.0.1")]
        [InlineData("127.0.0.1:503", "127.0.0.1:503")]
        [InlineData("[::1]:502", "::1")]
        [InlineData("[::1]:503", "[::1]:503")]
        public void CanParseEndpoint(string expectedString, string endpoint)
        {
            // Arrange
            var expected = IPEndPoint.Parse(expectedString);

            // Act
            var success = ModbusUtils.TryParseEndpoint(endpoint, out var actual);

            // Assert
            Assert.True(success);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CalculatesCrcCorrectly()
        {
            // Arrange
            var data = new byte[] { 0xA0, 0xB1, 0xC2 };

            // Act
            var expected = 0x2384;
            var actual = ModbusUtils.CalculateCRC(data);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SwapsEndiannessShort()
        {
            // Arrange
            var data = (short)512;

            // Act
            var expected = (short)2;
            var actual = ModbusUtils.SwitchEndianness(data);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SwapsEndiannessUShort()
        {
            // Arrange
            var data = (ushort)512;

            // Act
            var expected = (ushort)2;
            var actual = ModbusUtils.SwitchEndianness(data);

            // Assert
            Assert.Equal(expected, actual);
        }

        public static IList<object[]> GenericTestData => new List<object[]>
        {
            new object[] { new byte[] { 0x80, 0x90 }, new byte[] { 0x80, 0x90 } },
            new object[] { new short[] { 0x6040, 0x6141 }, new short[] { 0x4060, 0x4161 } },
            new object[] { new int[] { 0x60403020, 0x61413121 }, new int[] { 0x20304060, 0x21314161 } },
            new object[] { new long[] { 0x6040302010203040, 0x6141312111213141 }, new long[] { 0x4030201020304060, 0x4131211121314161 } },
            new object[] { new float[] { 0x60403020, 0x61413121 }, new float[] { 7.422001E+19F, 1.20603893E+21F } },
            new object[] { new double[] { 0x6040302010203040, 0x6141312111213141 }, new double[] { 1.0482134659314621E-250, 3.0464944316161389E+59 } }
        };

        [Theory]
        [MemberData(nameof(ModbusUtilsTests.GenericTestData))]
        public void SwapsEndiannessGeneric<T>(T[] dataset, T[] expected) where T : unmanaged
        {
            // Act
            ModbusUtils.SwitchEndianness(dataset.AsSpan());
            var actual = dataset;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SwapsEndiannessMidLittleEndian()
        {
            // Arrange
            var data = (uint)0x01020304;

            // Act
            var expected = (uint)0x02010403;
            var actual1 = ModbusUtils.ConvertBetweenLittleEndianAndMidLittleEndian(data);
            var actual2 = ModbusUtils.ConvertBetweenLittleEndianAndMidLittleEndian(actual1);

            // Assert
            Assert.Equal(expected, actual1);
            Assert.Equal(data, actual2);
        }
    }
}