using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModbusTCP.NET;
using System;
using System.Runtime.InteropServices;
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
            var server = new ModbusTcpServer(serverLogger, isAsynchronous: false);
            var client = new ModbusTcpClient();

            /* run Modbus TCP server (option 1) */
            var cts = new CancellationTokenSource();

            var task_server = Task.Run(() =>
            {
                server.Start();

                // update server buffer content once per second
                while (!cts.IsCancellationRequested)
                {
                    // lock is required to synchronize buffer access between this application and Modbus clients
                    lock (server.Lock)
                    {
                        DoServerWork(server);
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
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
            var byte_buffer = server.GetHoldingRegisterBuffer();
            byte_buffer[0] = (byte)(random.Next() >> 24);

            // interpret buffer as array of shorts (16 bit)
            var short_buffer = MemoryMarshal.Cast<byte, short>(server.GetHoldingRegisterBuffer());
            short_buffer[10] = (short)(random.Next(0, 100) >> 16);

            // interpret buffer as array of ints (32 bit)
            var int_buffer = MemoryMarshal.Cast<byte, int>(server.GetHoldingRegisterBuffer());
            int_buffer[20] = random.Next(0, 100);
        }

        static void DoClientWork(ModbusTcpClient client, ILogger logger)
        {
            Span<byte> data;

            var sleepTime = TimeSpan.FromMilliseconds(100);
            var unitIdentifier = (byte)0xFF;

            // ReadHoldingRegisters = 0x03,        // FC03
            data = client.ReadHoldingRegisters(unitIdentifier, 0, 10);
            logger.LogInformation("FC03 - ReadHoldingRegisters: Done");
            Thread.Sleep(sleepTime);

            // WriteMultipleRegisters = 0x10,      // FC16
            client.WriteMultipleRegisters(unitIdentifier, 0, new byte[] { 10, 00, 20, 00, 30, 00, 255, 00, 255, 01 });
            logger.LogInformation("FC16 - WriteMultipleRegisters: Done");
            Thread.Sleep(sleepTime);

            // ReadCoils = 0x01,                   // FC01
            data = client.ReadCoils(unitIdentifier, 0, 10);
            logger.LogInformation("FC01 - ReadCoils: Done");
            Thread.Sleep(sleepTime);

            // ReadDiscreteInputs = 0x02,          // FC02
            data = client.ReadDiscreteInputs(unitIdentifier, 0, 10);
            logger.LogInformation("FC02 - ReadDiscreteInputs: Done");
            Thread.Sleep(sleepTime);

            // ReadInputRegisters = 0x04,          // FC04
            data = client.ReadInputRegisters(unitIdentifier, 0, 10);
            logger.LogInformation("FC04 - ReadInputRegisters: Done");
            Thread.Sleep(sleepTime);

            // WriteSingleCoil = 0x05,             // FC05
            client.WriteSingleCoil(unitIdentifier, 0, true);
            logger.LogInformation("FC05 - WriteSingleCoil: Done");
            Thread.Sleep(sleepTime);

            // WriteSingleRegister = 0x06,         // FC06
            client.WriteSingleRegister(unitIdentifier, 0, new byte[] { 65, 67 });
            logger.LogInformation("FC06 - WriteSingleRegister: Done");
        }
    }
}
