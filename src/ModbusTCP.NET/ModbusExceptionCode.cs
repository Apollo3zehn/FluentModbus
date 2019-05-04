namespace ModbusTCP.NET
{
    public enum ModbusExceptionCode : byte
    {
        IllegalFunction = 0x01,
        IllegalDataAddress = 0x02,
        IllegalDataValue = 0x03,
        ServerDeviceFailure = 0x04,
        Acknowledge = 0x05,
        ServerDeviceBusy = 0x06,
        MemoryParityError = 0x8,
        GatewayPathUnavailable = 0x0A,
        GatewayTargetDeviceFailedToRespond = 0x0B,
    }
}
