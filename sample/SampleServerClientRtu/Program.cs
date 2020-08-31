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
            /* Modbus RTU uses a COM port for communication. Therefore, to run
             * this sample, you need to make sure that there are real or virtual 
             * COM ports available. The easiest way is to install one of the free
             * COM port bridges available in the internet. That way, the Modbus 
             * server can connect to e.g. COM1 which is virtually linked to COM2,
             * where the client is connected to.
             * 
             * When you only want to use the client and communicate to an external
             * Modbus server, simply remove all server related code parts in this 
             * sample and connect to real COM port using only the client.
             */

            /* define COM ports */
            var serverPort = "COM1";
            var clientPort = "COM2";

            /* create logger */
            var loggerFactory = LoggerFactory.Create(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                loggingBuilder.AddConsole();
            });

            var serverLogger = loggerFactory.CreateLogger("Server");
            var clientLogger = loggerFactory.CreateLogger("Client");

            /* create Modbus RTU server and client */
            var server = new ModbusRtuServer(unitIdentifier: 1);
            var client = new ModbusRtuClient();

            /* run Modbus RTU server */
            var cts = new CancellationTokenSource();

            var task_server = Task.Run(async () =>
            {
                server.Start(serverPort);
                serverLogger.LogInformation("Server started.");

                while (!cts.IsCancellationRequested)
                {
                    // lock is required to synchronize buffer access between this application and the Modbus client
                    lock (server.Lock)
                    {
                        DoServerWork(server);
                    }

                    // update server buffer content once per second
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                server.Dispose();
            }, cts.Token);

            /* run Modbus RTU client */
            var task_client = Task.Run(() =>
            {
                client.Connect(clientPort);

                try
                {
                    DoClientWork(client, clientLogger);
                }
                catch (Exception ex)
                {
                    clientLogger.LogError(ex.Message);
                }

                client.Close();

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

        static void DoServerWork(ModbusRtuServer server)
        {
            var random = new Random();

            // Option A: normal performance version, more flexibility

                /* get buffer in standard form (Span<short>) */
                var registers = server.GetHoldingRegisters();
                registers.SetValueLittleEndian(startingAddress: 5, random.Next());

            // Option B: high performance version, less flexibility

                /* interpret buffer as array of bytes (8 bit) */
                var byte_buffer = server.GetHoldingRegisterBuffer<byte>();
                byte_buffer[20] = (byte)(random.Next() >> 24);

                /* interpret buffer as array of shorts (16 bit) */
                var short_buffer = server.GetHoldingRegisterBuffer<short>();
                short_buffer[30] = (short)(random.Next(0, 100) >> 16);

                /* interpret buffer as array of ints (32 bit) */
                var int_buffer = server.GetHoldingRegisterBuffer<int>();
                int_buffer[40] = random.Next(0, 100);
        }

        static void DoClientWork(ModbusRtuClient client, ILogger logger)
        {
            Span<byte> data;

            var sleepTime = TimeSpan.FromMilliseconds(100);
            var unitIdentifier = (byte)0x01;
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
