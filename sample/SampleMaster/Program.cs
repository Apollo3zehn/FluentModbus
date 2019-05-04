using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModbusTCP.NET;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SampleMaster
{
    class Program
    {
        static async Task Main(string[] args)
        {
            /* prepare dependency injection */
            var services = new ServiceCollection();

            ConfigureServices(services);

            /* create types */
            var provider = services.BuildServiceProvider();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var serverLogger = loggerFactory.CreateLogger("Server");
            var clientLogger = loggerFactory.CreateLogger("Client");

            /* create Modbus TCP server and client */
            var server = new ModbusTcpServer(serverLogger);
            var client = new ModbusTcpClient();

            /* run Modbus TCP server */
            var cts = new CancellationTokenSource();

            var task_server = Task.Run(async () =>
            {
                server.Start();

                while (!cts.IsCancellationRequested)
                {
                    if (server.IsReady)
                    {
                        server.Update();
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(20));
                }
            }, cts.Token);

            /* run Modbus TCP client */
            var task_client = Task.Run(() =>
            {
                client.Connect();

                try
                {
                    Program.PerformRequests(client, clientLogger);
                }
                catch (Exception ex)
                {
                    clientLogger.LogError(ex.Message);
                    throw;
                }

                client.Disconnect();
                clientLogger.LogInformation("Tests finished.");
            });

            // wait for client task to finish
            await task_client;

            // stop server
            cts.Cancel();
            await task_server;
            server.Stop();
            serverLogger.LogInformation("Server stopped.");

            /* wait for stop signal */
            Console.ReadKey(true);
        }

        static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                loggingBuilder.AddConsole();
            });
        }

        static void PerformRequests(ModbusTcpClient client, ILogger logger)
        {
            Span<byte> data;

            var sleepTime = TimeSpan.FromMilliseconds(100);
            var unitIdentifier = (byte)0xFF;
            var modbusTcpMessageBuffer = new ModbusTcpMessageBuffer();

            try
            {
                // ReadHoldingRegisters = 0x03,        // FC03
                data = client.ReadHoldingRegisters(unitIdentifier, 0, 10, modbusTcpMessageBuffer);
                logger.LogInformation("FC03 - ReadHoldingRegisters: Done");
                Thread.Sleep(sleepTime);

                // WriteMultipleRegisters = 0x10,      // FC16
                client.WriteMultipleRegisters(unitIdentifier, 0, new byte[] { 10, 00, 20, 00, 30, 00, 255, 00, 255, 01 }, modbusTcpMessageBuffer);
                logger.LogInformation("FC16 - WriteMultipleRegisters: Done");
                Thread.Sleep(sleepTime);

                // ReadCoils = 0x01,                   // FC01
                data = client.ReadCoils(unitIdentifier, 0, 10, modbusTcpMessageBuffer);
                logger.LogInformation("FC01 - ReadCoils: Done");
                Thread.Sleep(sleepTime);

                // ReadDiscreteInputs = 0x02,          // FC02
                data = client.ReadDiscreteInputs(unitIdentifier, 0, 10, modbusTcpMessageBuffer);
                logger.LogInformation("FC02 - ReadDiscreteInputs: Done");
                Thread.Sleep(sleepTime);

                // ReadInputRegisters = 0x04,          // FC04
                data = client.ReadInputRegisters(unitIdentifier, 0, 10, modbusTcpMessageBuffer);
                logger.LogInformation("FC04 - ReadInputRegisters: Done");
                Thread.Sleep(sleepTime);

                // WriteSingleCoil = 0x05,             // FC05
                client.WriteSingleCoil(unitIdentifier, 0, true, modbusTcpMessageBuffer);
                logger.LogInformation("FC05 - WriteSingleCoil: Done");
                Thread.Sleep(sleepTime);

                // WriteSingleRegister = 0x06,         // FC06
                client.WriteSingleRegister(unitIdentifier, 0, new byte[] { 65, 67 }, modbusTcpMessageBuffer);
                logger.LogInformation("FC06 - WriteSingleRegister: Done");
            }
            finally
            {
                modbusTcpMessageBuffer.Dispose();
            }
        }
    }
}
