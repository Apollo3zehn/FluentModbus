# FluentModbus

[![GitHub Actions](https://github.com/Apollo3zehn/FluentModbus/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/Apollo3zehn/FluentModbus/actions) [![NuGet](https://img.shields.io/nuget/v/FluentModbus.svg?label=Nuget)](https://www.nuget.org/packages/FluentModbus)

FluentModbus is a .NET Standard library (2.0 and 2.1) that provides Modbus TCP/RTU server and client implementations for easy process data exchange. Both, the server and the client, implement class 0, class 1 and class 2 (partially) functions of the [specification](http://www.modbus.org/specs.php). Namely, these are:

#### Class 0:
* FC03: ReadHoldingRegisters
* FC16: WriteMultipleRegisters

#### Class 1:
* FC01: ReadCoils
* FC02: ReadDiscreteInputs
* FC04: ReadInputRegisters
* FC05: WriteSingleCoil
* FC06: WriteSingleRegister

#### Class 2:
* FC15: WriteMultipleCoils
* FC23: ReadWriteMultipleRegisters

Please see the [introduction](https://apollo3zehn.github.io/FluentModbus/) to get a more detailed description on how to use this library!

Below is a screenshot of the [sample](https://apollo3zehn.github.io/FluentModbus/samples/modbus_tcp.html) console output using a Modbus TCP server and client:

![Sample.](/doc/images/sample.png)