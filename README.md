# Modbus.NET

[![AppVeyor](https://ci.appveyor.com/api/projects/status/github/apollo3zehn/modbustcp.net?svg=true)](https://ci.appveyor.com/project/Apollo3zehn/modbustcp-net)

based on http://www.modbus.org/specs.php:

* MODBUS MESSAGING ON TCP/IP IMPLEMENTATION GUIDE V1.0b 
* MODBUS APPLICATION PROTOCOL SPECIFICATION V1.1b3

```cs
modbusClient.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 502));
```