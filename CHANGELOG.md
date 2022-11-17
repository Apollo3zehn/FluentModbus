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