# Modbus Validator

The following code is an extension to the [Modbus TCP sample](modbus_tcp.md) but is just as valid for the RTU server. The `RequestValidator` property accepts a method which performs the validation each time a client sends a request to the server. This function will not replace the default checks but complement it. This means for example that the server will still check if the absolute registers limits are exceeded or an unknown function code is provided.

```cs
var server = new ModbusTcpServer()
{
    RequestValidator = this.ModbusValidator;
};

private ModbusExceptionCode ModbusValidator(RequestValidatorArgs args)
{
    // check if address is within valid holding register limits
    var holdingLimits = args.Address >= 50 && args.Address < 90 ||
                        args.Address >= 2000 && args.Address < 2100;

    // check if address is within valid input register limits
    var inputLimits = args.Address >= 1000 && args.Address < 2000;

    // go through all cases and return proper response
    return (args.FunctionCode, holdingLimits, inputLimits) switch
    {
        // holding registers
        (ModbusFunctionCode.ReadHoldingRegisters, true, _)          => ModbusExceptionCode.OK,
        (ModbusFunctionCode.ReadWriteMultipleRegisters, true, _)    => ModbusExceptionCode.OK,
        (ModbusFunctionCode.WriteMultipleRegisters, true, _)        => ModbusExceptionCode.OK,
        (ModbusFunctionCode.WriteSingleRegister, true, _)           => ModbusExceptionCode.OK,

        (ModbusFunctionCode.ReadHoldingRegisters, false, _)         => ModbusExceptionCode.IllegalDataAddress,
        (ModbusFunctionCode.ReadWriteMultipleRegisters, false, _)   => ModbusExceptionCode.IllegalDataAddress,
        (ModbusFunctionCode.WriteMultipleRegisters, false, _)       => ModbusExceptionCode.IllegalDataAddress,
        (ModbusFunctionCode.WriteSingleRegister, false, _)          => ModbusExceptionCode.IllegalDataAddress,

        // input registers
        (ModbusFunctionCode.ReadInputRegisters, _, true)            => ModbusExceptionCode.OK,
        (ModbusFunctionCode.ReadInputRegisters, _, false)           => ModbusExceptionCode.IllegalDataAddress,

        // deny other function codes
        _                                                           => ModbusExceptionCode.IllegalFunction
    };
}
```