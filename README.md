# ModbusTCP.NET

[![AppVeyor](https://ci.appveyor.com/api/projects/status/github/apollo3zehn/modbus.net?svg=true)](https://ci.appveyor.com/project/Apollo3zehn/modbus-net)

ModbusTCP.NET is a .NET Standard library that provides a Modbus TCP server and client implementation for easy process data exchange. Both, the server and the client, implement class 0 and class 1 functions of the [specification](http://www.modbus.org/specs.php). Namely, these are:

#### Class 0:
* FC03: ReadHoldingRegisters
* FC16: WriteMultipleRegisters

#### Class 1:
* FC01: ReadCoils
* FC02: ReadDiscreteInputs
* FC04: ReadInputRegisters
* FC05: WriteSingleCoil
* FC06: WriteSingleRegister

Please see the following introduction and the [sample](sample/SampleServerClient) application, to get started with ModbusTCP.NET.

### A few words to ```Span<T>```

The returned data of the read functions (FC01 to FC04) are always provided as ```Span<T>``` ([What is this?](https://msdn.microsoft.com/en-us/magazine/mt814808.aspx)). In short, a ```Span<T>``` is a simple view of the underlying memory. With this type, the memory can be interpreted as ```byte```, ```int```, ```float``` or any other value type. A conversion from ```Span<byte>``` to other types can be efficiently achieved through:

```cs
Span<byte> byteSpan = new byte[] { 1, 2, 3, 4 }.AsSpan();
Span<int> intSpan = MemoryMarshal.Cast<byte, int>(byteSpan);
Span<float> floatSpan = MemoryMarshal.Cast<int, float>(intSpan);
```

You can then access it like a any other array:

```cs
var floatValue = myFloatSpan[0];
```

The data remain unchanged during all of these calls. _Only the interpretation changes._ However, one disadvantage is that this type cannot be used in all code locations (e.g. in ```async``` functions). Therefore, if you run into these limitations, you can simply convert the returned data to a plain array (which is essentially a copy operation):

```cs
float[] floatArray = floatSpan.ToArray();
```

## Creating a Modbus TCP client

A new Modbus TCP client can be easily created with the following code:

```cs
var client = new ModbusTcpClient();
```

Once you have an instance, connect to a server in one of the following ways:

```cs
// use default IP address 127.0.0.1 and port 502
client.Connect();

// use specified IP address and default port 502
client.Connect(IPAddress.Parse("127.0.0.1"));

// use specified IP adress and port
client.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 502))
```

### Reading data

#### Reading integer or float

First, define the unit identifier, the starting address and the number of values to read (count):

```cs
var unitIdentifier = (byte)0xFF; // 0x00 and 0xFF are the defaults for TCP/IP only Modbus devices.
var startingAddress = (ushort)0;
var count = (ushort)10;
```

Then, read the data:

```cs
var shortData = client.ReadHoldingRegisters<short>(unitIdentifier, startingAddress, count);
```

As explained above, you can _interpret_ the data in different ways using the generic overloads, which does the ```MemoryMarshal.Cast<T1, T2>``` work for you:

```cs
// interpret data as float
var floatData = client.ReadHoldingRegisters<float>(unitIdentifier, startingAddress, count);
var firstValue = floatData[0];
var lastValue = floatData[floatData.Length - 1];

Console.WriteLine($"Fist value is {firstValue}");
Console.WriteLine($"Last value is {lastValue}");
```

If you want to keep the data for later use or you want to use the Modbus TCP client in asynchronous methods, convert the ```Span<T>``` into a normal array with ```ToArray()```:

```cs
async byte[] DoAsync()
{
    var client = new ModbusTcpClient();
    client.Connect(...);

    await <awaitsomething>;

    return client.ReadHoldingRegisters(1, 2, 3).ToArray();
}
```

> **Note:** The generic overloads shown here are intended for normal use. Compared to that, the non-generic overloads like ```client.ReadHoldingRegisters()``` have slightly better performance. However, they achieve this by doing fewer checks and conversions. This means, these methods are less convenient to use and only recommended in high-performance scenarios, where raw data (i.e. byte arrays) are moved around.

#### Reading boolean

Boolean values are returned as single bits (1 = true, 0 = false), which are packed into bytes. If you request 10 booleans you get a ```Span<byte>``` in return with a length of ```2``` bytes. In this example, the remaining ```6``` bits are fill values.

```cs
var unitIdentifier = (byte)0xFF;
var startingAddress = (ushort)0;
var quantity = (ushort)10;

var boolData = client.ReadCoils(unitIdentifier, startingAddress, quantity);
```

You can check if a certain bit (here: ```bit 2```) is set with:

```cs
var position = 2;
var boolValue = ((boolData[0] >> position) & 1) > 0;
```

See also [this](https://stackoverflow.com/questions/47981/how-do-you-set-clear-and-toggle-a-single-bit) overview to understand how to manipulate single bits.

### Writing data

#### Writing integer or float

The following example shows how to write the number ```4263``` to the server:

```cs
var unitIdentifier = (byte)0xFF;
var startingAddress = (ushort)0;
var registerAddress = (ushort)0;
var quantity = (ushort)10;

var shortData = new short[] { 4263 };
client.WriteSingleRegister(unitIdentifier, registerAddress, shortData);

// read back from server to prove correctness
var shortDataResult = client.ReadHoldingRegisters<short>(unitIdentifier, startingAddress, 1);
Console.WriteLine(shortDataResult[0]); // should print '4263'
```

> **Note**: The Modbus protocol defines a basic register size of 2 bytes. Thus, the write methods require input values (or arrays) with even number of bytes (2, 4, 6, ...). This means that a call to ```client.WriteSingleRegister(0, 0, new byte { 1 })``` will not work, but ```client.WriteSingleRegister(0, 0, new short { 1 })``` will do. Since the client validates all your inputs (and so the server does), you will get notified if anything is wrong.

If you want to write float values, the procedure is the same as shown previously using the generic overload:

```cs
var floatData = new float[] { 1.1F, 9557e3F };
client.WriteMultipleRegisters(unitIdentifier, startingAddress, floatData);
```

#### Writing boolean

It's as simple as:

```cs
client.WriteSingleCoil(unitIdentifier, registerAddress, true);
```

## Creating a Modbus TCP server

First, you need to instantiate the Modbus TCP server:

```cs
var server = new ModbusTcpServer();
```

Then you can start it:

```cs
server.Start();
```

### Option 1 (asynchronous operation)

There are two options to operate the server. The first one, which is the default, is asynchronous operation. This means all client requests are handled immediately. However, asynchronous operation requires a synchronization of data access, which can be accomplished using the ```lock``` keyword:

```cs
var random = new Random();
var cts = new CancellationTokenSource();

while (!cts.IsCancellationRequested)
{
    var intData = server.GetHoldingRegisterBuffer<int>();

    // lock is required to synchronize buffer access between
    // this application and one or more Modbus clients
    lock (server.Lock)
    {
        intData[20] = random.Next(0, 100);
    }

    // update server buffer content only once per second
    await Task.Delay(TimeSpan.FromSeconds(1));
}
```

### Option 2 (synchronous operation)

The second mode is the _synchronous_ mode, which is useful for advanced scenarios, where a lock mechanism is undesirable. In this mode, the hosting application is responsible to trigger the data update method (```server.Update()```) regularly:

```cs
var random = new Random();
var cts = new CancellationTokenSource();

while (!cts.IsCancellationRequested)
{
    var intData = server.GetHoldingRegisterBuffer<int>();
    intData[20] = random.Next(0, 100);

    server.Update();

    await Task.Delay(TimeSpan.FromMilliseconds(100));
}
```

Note that in the second example, the ```Task.Delay()``` period is much lower. Since we want coordinated access between the application and the clients _without_ locks, we need to ensure that at certain points in time, the application is safe to access the buffers. This is the case when the ```IsReady``` propery is ```true``` (when all client requests have been served). After the application finished manipulating the server data, it triggers the server to serve all accumulated client requests (via the ```Update()``` method). Finally, the process repeats.

## See also

This implementation is based on http://www.modbus.org/specs.php:

* MODBUS APPLICATION PROTOCOL SPECIFICATION V1.1b3
* MODBUS MESSAGING ON TCP/IP IMPLEMENTATION GUIDE V1.0b 
