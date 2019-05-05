# Modbus.NET

[![AppVeyor](https://ci.appveyor.com/api/projects/status/github/apollo3zehn/modbus.net?svg=true)](https://ci.appveyor.com/project/Apollo3zehn/modbus-net)

ModbusTCP.NET is a library to create a Modbus TCP server or client for easy process data exchange. Both, the server and the client, implement class 0 and class 1 functions of the specification. Namely, these are:

#### Class 0:
* FC03: ReadHoldingRegisters
* FC16: WriteMultipleRegisters

#### Class 1:
* FC01: ReadCoils
* FC02: ReadDiscreteInputs
* FC04: ReadInputRegisters
* FC05: WriteSingleCoil
* FC06: WriteSingleRegister

## Creating a Modbus TCP client

Modbus TCP client can be easily created with the following code:

```cs
var client = new ModbusTcpClient();
```

Once you have an instance, connect to a server with one of the following ways:

```cs
client.Connect(); // defaults to IP 127.0.0.1 and port 502
client.Connect(IPAddress.Parse("127.0.0.1")); // uses specified IP address and default port 502
client.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 502)) // uses specified IP adress and port
```

### Reading data

```cs
var unitIdentifier = (byte)0;
var startingAddress = (byte)0;
var quantity = (ushort)10;

/* interpret data as byte */
var byteData = client.ReadHoldingRegisters(unitIdentifier, startingAddress, quantity);
var firstByteValue = byteData[0];

/* interpret data as short */
var shortData = MemoryMarshal.Cast<byte, short>(client.ReadHoldingRegisters(unitIdentifier, startingAddress, quantity));
var firstShortValue = shortData[0];
var lastShortValue = shortData[shortData.Length - 1];

Console.WriteLine($"Value of first of {quantity} returned values on starting address {startingAddress} is {firstShortValue}");
Console.WriteLine($"Value of last of {quantity} returned values on starting address {startingAddress} is {lastShortValue}");

/* preserve data for later use */
var buffer = new byte[quantity * 2];
byteData.CopyTo(buffer);
```

### Writing data

## Creating a Modbus TCP server

## See also

This implementation is based on http://www.modbus.org/specs.php:

* MODBUS MESSAGING ON TCP/IP IMPLEMENTATION GUIDE V1.0b 
* MODBUS APPLICATION PROTOCOL SPECIFICATION V1.1b3