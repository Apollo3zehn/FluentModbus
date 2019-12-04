using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus.SampleMaster
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
                serverLogger.LogInformation("Server started.");

                while (!cts.IsCancellationRequested)
                {
                    // lock is required to synchronize buffer access between this application and one or more Modbus clients
                    lock (server.Lock)
                    {
                        DoServerWork(server);
                    }

                    // update server buffer content once per second
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                server.Dispose();
            }, cts.Token);

            /* run Modbus TCP client */
            var task_client = Task.Run(() =>
            {
                client.Connect();

                try
                {
                    DoClientWork(client, clientLogger);
                }
                catch (Exception ex)
                {
                    clientLogger.LogError(ex.Message);
                }

                client.Disconnect();

                Console.WriteLine("Tests finished. Press any key to continue.");
                Console.ReadKey(true);
            });

            // wait for client task to finish
            await task_client;

            // stop server
            cts.Cancel();
            await task_server;

            server.Stop();
            serverLogger.LogInformation("Server stopped.");
        }

        static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                loggingBuilder.AddConsole();
            });
        }

        static void DoServerWork(ModbusTcpServer server)
        {
            var random = new Random();

            // interpret buffer as array of bytes (8 bit)
            var byte_buffer = server.GetHoldingRegisterBuffer<byte>();
            byte_buffer[20] = (byte)(random.Next() >> 24);

            // interpret buffer as array of shorts (16 bit)
            var short_buffer = server.GetHoldingRegisterBuffer<short>();
            short_buffer[30] = (short)(random.Next(0, 100) >> 16);

            // interpret buffer as array of ints (32 bit)
            var int_buffer = server.GetHoldingRegisterBuffer<int>();
            int_buffer[40] = random.Next(0, 100);
        }

        static void DoClientWork(ModbusTcpClient client, ILogger logger)
        {
            Span<byte> data;

            var sleepTime = TimeSpan.FromMilliseconds(100);
            var unitIdentifier = (byte)0xFF;
            var startingAddress = (ushort)0;
            var registerAddress = (ushort)0;

            // ReadHoldingRegisters = 0x03,        // FC03
            data = client.ReadHoldingRegisters(unitIdentifier, startingAddress, 10);
            logger.LogInformation("FC03 - ReadHoldingRegisters: Done");
            Thread.Sleep(sleepTime);

            // WriteMultipleRegisters = 0x10,      // FC16
            client.WriteMultipleRegisters(unitIdentifier, startingAddress, new byte[] { 10, 00, 20, 00, 30, 00, 255, 00, 255, 01 });
            logger.LogInformation("FC16 - WriteMultipleRegisters: Done");
            Thread.Sleep(sleepTime);

            // ReadCoils = 0x01,                   // FC01
            data = client.ReadCoils(unitIdentifier, startingAddress, 10);
            logger.LogInformation("FC01 - ReadCoils: Done");
            Thread.Sleep(sleepTime);

            // ReadDiscreteInputs = 0x02,          // FC02
            data = client.ReadDiscreteInputs(unitIdentifier, startingAddress, 10);
            logger.LogInformation("FC02 - ReadDiscreteInputs: Done");
            Thread.Sleep(sleepTime);

            // ReadInputRegisters = 0x04,          // FC04
            data = client.ReadInputRegisters(unitIdentifier, startingAddress, 10);
            logger.LogInformation("FC04 - ReadInputRegisters: Done");
            Thread.Sleep(sleepTime);

            // WriteSingleCoil = 0x05,             // FC05
            client.WriteSingleCoil(unitIdentifier, registerAddress, true);
            logger.LogInformation("FC05 - WriteSingleCoil: Done");
            Thread.Sleep(sleepTime);

            // WriteSingleRegister = 0x06,         // FC06
            client.WriteSingleRegister(unitIdentifier, registerAddress, new byte[] { 65, 67 });
            logger.LogInformation("FC06 - WriteSingleRegister: Done");
        }
    }
}
