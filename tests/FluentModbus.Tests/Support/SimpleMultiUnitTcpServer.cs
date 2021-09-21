using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FluentModbus.Tests
{
    internal class SimpleMultiUnitTcpServer : ModbusTcpServer
    {
        public SimpleMultiUnitTcpServer()
        {
            this.RemoveUnit(0);
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
                while (!this.CTS.IsCancellationRequested)
                {
                    // There are no default timeouts (SendTimeout and ReceiveTimeout = 0), 
                    // use ConnectionTimeout instead.
                    var tcpClient = await tcpClientProvider.AcceptTcpClientAsync();
                    var requestHandler = new ModbusTcpRequestHandler(tcpClient, this, handleUnitIdentifiers: true);

                    lock (this.Lock)
                    {
                        if (this.MaxConnections > 0 &&
                            /* request handler is added later in 'else' block, so count needs to be increased by 1 */
                            this.RequestHandlers.Count + 1 > this.MaxConnections)
                        {
                            tcpClient.Close();
                        }
                        else
                        {
                            this.RequestHandlers.Add(requestHandler);
                        }
                    }
                }
            }, this.CTS.Token);
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
