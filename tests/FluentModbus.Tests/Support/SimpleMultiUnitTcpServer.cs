using System.Net;

namespace FluentModbus.Tests
{
    internal class SimpleMultiUnitTcpServer : ModbusTcpServer
    {
        public SimpleMultiUnitTcpServer()
        {
            RemoveUnit(0);
        }

        public void StartMultiUnit(IPEndPoint localEndpoint)
        {
            var tcpClientProvider = new DefaultTcpClientProvider(localEndpoint);

            base.StopProcessing();
            base.StartProcessing();

            var requestHandlers = new List<ModbusTcpRequestHandler>();

            // accept clients asynchronously
            /* https://stackoverflow.com/questions/2782802/can-net-task-instances-go-out-of-scope-during-run */
            Task.Run(async () =>
            {
                while (!CTS.IsCancellationRequested)
                {
                    // There are no default timeouts (SendTimeout and ReceiveTimeout = 0), 
                    // use ConnectionTimeout instead.
                    var tcpClient = await tcpClientProvider.AcceptTcpClientAsync();
                    var requestHandler = new ModbusTcpRequestHandler(tcpClient, this);

                    lock (Lock)
                    {
                        if (MaxConnections > 0 &&
                            /* request handler is added later in 'else' block, so count needs to be increased by 1 */
                            RequestHandlers.Count + 1 > MaxConnections)
                        {
                            tcpClient.Close();
                        }
                        else
                        {
                            RequestHandlers.Add(requestHandler);
                        }
                    }
                }
            }, CTS.Token);
        }

        public new void AddUnit(byte unitIdentifer)
        {
            base.AddUnit(unitIdentifer);
        }

        public new void RemoveUnit(byte unitIdentifer)
        {
            base.RemoveUnit(unitIdentifer);
        }
    }
}
