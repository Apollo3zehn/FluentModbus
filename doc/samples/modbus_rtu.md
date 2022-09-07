# Modbus RTU

The following sample show how a ModbusRTU [server](xref:FluentModbus.ModbusRtuServer) and [client](xref:FluentModbus.ModbusRtuClient) can interact with each other. The full sample is located [here](https://github.com/Apollo3zehn/FluentModbus/blob/master/sample/SampleServerClientRtu/Program.cs).

## Notes

> [!NOTE]
> In this sample, the server runs in ```asynchronous``` mode, i.e. the client request is answered immediately. Since this can potentially slow down the server when there are too many requests within short time, there is also the ```synchronous``` operating mode, which can be opted-in via ```new ModbusRtuServer(isAsynchronous: true)```. When you do so, the accumulated client requests are processed with a call to ```server.Update()```. See the [introduction](../index.md) for a short sample.

## Setup

First, create the server and client instances and a logger to print the progress to the console:

```cs
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

    /* create Modbus RTU server */
    using var server = new ModbusRtuServer(unitIdentifier: 1)
    {
        // see 'RegistersChanged' event below
        EnableRaisingEvents = true
    };

    /* subscribe to the 'RegistersChanged' event (in case you need it) */
    server.RegistersChanged += (sender, registerAddresses) =>
    {
        // the variable 'registerAddresses' contains a list of modified register addresses
    };

    /* create Modbus RTU client */
    var client = new ModbusRtuClient();
```

When everything is prepared, first start the server ...

```cs

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
    }, cts.Token);

```

... and then the client:

```cs

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
        Console.ReadKey(intercept: true);
    });

```

Finally, wait for the the client to finish its work and then stop the server using the cancellation token source.

```cs
    // wait for client task to finish
    await task_client;

    // stop server
    cts.Cancel();
    await task_server;

    server.Stop();
    serverLogger.LogInformation("Server stopped.");
}

```

## Server operation

While the server runs, the method below is executed periodically once per second. This method shows how the server can update it's registers to provide new data to the clients. 

```cs
static void DoServerWork(ModbusRtuServer server)
{
    var random = new Random();

    // Option A: normal performance version, more flexibility

        /* get buffer in standard form (Span<short>) */
        var registers = server.GetHoldingRegisters();
        registers.SetLittleEndian<int>(startingAddress: 5, random.Next());

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

```

# Client operation

When the client is started, the method below is executed only once. After that, the client stops and the user is prompted to enter any key to finish the program.

> [!NOTE]
> All data is returned as ```Span<T>```, which has great advantages but also some limitations as described in the repository's README. If you run into these limitations, simply call ```ToArray()``` to copy the data into a standard .NET array (e.g. ```var newData = data.ToArray();```)

> [!NOTE]
> Remember to always use the correct unit identifier (here: ```0x01```), which was assigned to the server when it was instantiated.

```cs
static void DoClientWork(ModbusRtuClient client, ILogger logger)
{
    Span<byte> data;

    var sleepTime = TimeSpan.FromMilliseconds(100);
    var unitIdentifier = 0x01;
    var startingAddress = 0;
    var registerAddress = 0;

    // ReadHoldingRegisters = 0x03,        // FC03
    data = client.ReadHoldingRegisters<byte>(unitIdentifier, startingAddress, 10);
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
    data = client.ReadInputRegisters<byte>(unitIdentifier, startingAddress, 10);
    logger.LogInformation("FC04 - ReadInputRegisters: Done");
    Thread.Sleep(sleepTime);

    // WriteSingleCoil = 0x05,             // FC05
    client.WriteSingleCoil(unitIdentifier, registerAddress, true);
    logger.LogInformation("FC05 - WriteSingleCoil: Done");
    Thread.Sleep(sleepTime);

    // WriteSingleRegister = 0x06,         // FC06
    client.WriteSingleRegister(unitIdentifier, registerAddress, 127);
    logger.LogInformation("FC06 - WriteSingleRegister: Done");
}
```