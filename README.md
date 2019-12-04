# FluentModbus

[![AppVeyor](https://ci.appveyor.com/api/projects/status/github/apollo3zehn/fluentmodbus?svg=true&branch=master)](https://ci.appveyor.com/project/Apollo3zehn/fluentmodbus)
[![NuGet](https://img.shields.io/nuget/v/FluentModbus.svg?label=Nuget)](https://www.nuget.org/packages/FluentModbus)

FluentModbus is a .NET Standard library (2.0 and 2.1) that provides a Modbus TCP server and client implementation for easy process data exchange. Both, the server and the client, implement class 0 and class 1 functions of the [specification](http://www.modbus.org/specs.php). Namely, these are:

#### Class 0:
* FC03: ReadHoldingRegisters
* FC16: WriteMultipleRegisters

#### Class 1:
* FC01: ReadCoils
* FC02: ReadDiscreteInputs
* FC04: ReadInputRegisters
* FC05: WriteSingleCoil
* FC06: WriteSingleRegister

Please see the [introduction](https://apollo3zehn.github.io/ImcFamosFile/how_to/1_introduction.html) to get a more detailed description on how to use this library!

Below is a screenshot of the [sample](samples/modbus_tcp.md) console output using a Modbus TCP server and client:

![Sample.](/doc/images/sample.png)