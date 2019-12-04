using Xunit;

namespace FluentModbus.Tests
{
    public class ModbusUtilsTests : IClassFixture<XUnitFixture>
    {
        [Fact]
        public void CalculatesCrcCorrectly()
        {
            // Arrange
            var data = new byte[] { 0xA0, 0xB1, 0xC2 };

            // Act
            var expected = 0x2384;
            var actual = ModbusUtils.CalculateCRC(data);

            // Assert
            Assert.True(actual == expected);
        }
    }
}