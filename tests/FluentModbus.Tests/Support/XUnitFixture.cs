namespace FluentModbus
{
    public class XUnitFixture
    {
        public XUnitFixture()
        {
            ModbusTcpServer.DefaultConnectionTimeout = TimeSpan.FromSeconds(10);
            ModbusTcpClient.DefaultConnectTimeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
        }

        public void Dispose()
        {
            //
        }
    }
}
