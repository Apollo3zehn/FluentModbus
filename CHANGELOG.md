## v5.3.0 - 2024-09-19

### Features
- Implement `ModbusRtuOverTcpClient.cs` (#99) Thanks @schotime!

## v5.2.0 - 2024-04-23

### Features
- Make IsConnected an abstract member of ModbusClient (#115)

## v5.1.0 - 2024-02-21

### Features
- Add option for raising event even if values of buffer have not changed (#96) 
- support for WriteMultipleCoils (#111)

### Bugs Fixed
- Fixed propagation of cancellationToken (#100)
- Fixed exception for malformed messages (#101)
- typo in ModbusClient docstring (#95)
- SampleServerClientTCP broken? (#102)

## v5.0.3 - 2023-08-03

- The Modbus TCP server now returns the received unit identifier even when its own unit identifier is set to zero (the default) (solves #93).
- The protected methods `AddUnit()` and `RemoveUnit()` have been made public.

## v5.0.2 - 2022-11-14

### Bugs Fixed

- The Modbus RTU client did not correctly detect response frames (thanks @zhangchaoza, fixes https://github.com/Apollo3zehn/FluentModbus/issues/83)

## v5.0.1 - 2022-11-14

### Bugs Fixed

- The Modbus RTU server did not correctly detect request frames (thanks @jmsqlr, https://github.com/Apollo3zehn/FluentModbus/pull/75#issuecomment-1304653670)

## v5.0.0 - 2022-09-08

### Breaking Changes
- The previously introduced TCP client constructor overload was called `Connect` although it expected a totally externally managed TCP client which should already be connected. This constructor is now named `Initialize` and its signature has been adapted to better fit its purpose. The passed TCP client (or `IModbusRtuSerialPort` in case of the RTU client) is now not modified at all, i.e. configured timeouts or other things are not applied to these externally managed instances (#78).

### Features
- Modbus TCP and RTU clients implement `IDisposable` so you can do the following now: `using var client = new ModbusTcpClient(...)` (#67)
- Modbus server base class has now a virtual `Stop` method so the actual server can be stopped using a base class reference (#79).

### Bugs Fixed
- The Modbus server ignored the unit identifier and responded to all requests (#79).
- Modbus server side read timeout exception handling is more defined now: 
    - The TCP server closes the connection.
    - The Modbus RTU server ignores the exception as there is only a single connection and if that one is closed, there would be no point in keeping the RTU server running.
- Modbus server did not properly handle asynchronous cancellation (#79).

> [See API changes on Fuget.org](https://www.fuget.org/packages/FluentModbus/5.0.0/lib/netstandard2.1/diff/4.1.0/)

Thanks @schotime and @LukasKarel for your PRs!