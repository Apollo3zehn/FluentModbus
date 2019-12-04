namespace FluentModbus
{
    /// <summary>
    /// Specifies the Modbus exception type.
    /// </summary>
    public enum ModbusExceptionCode : byte
    {
        /// <summary>
        /// The function code received in the query is not an allowable action for the server.
        /// </summary>
        IllegalFunction = 0x01,

        /// <summary>
        /// The data address received in the query is not an allowable address for the server.
        /// </summary>
        IllegalDataAddress = 0x02,

        /// <summary>
        /// A value contained in the query data field is not an allowable value for server.
        /// </summary>
        IllegalDataValue = 0x03,

        /// <summary>
        /// An unrecoverable error occurred while the server was attempting to perform the requested action.
        /// </summary>
        ServerDeviceFailure = 0x04,

        /// <summary>
        /// Specialized use in conjunction with programming commands. The server has accepted the request and is processing it, but a long duration of time will be required to do so.
        /// </summary>
        Acknowledge = 0x05,

        /// <summary>
        /// Specialized use in conjunction with programming commands. The engaged in processing a long–duration program command.
        /// </summary>
        ServerDeviceBusy = 0x06,

        /// <summary>
        /// Specialized use in conjunction with function codes 20 and 21 and reference type 6, to indicate that the extended file area failed to pass a consistency check.
        /// </summary>
        MemoryParityError = 0x8,

        /// <summary>
        /// Specialized use in conjunction with gateways, indicates that the gateway was unable to allocate an internal communication path from the input port to the output port for processing the request.
        /// </summary>
        GatewayPathUnavailable = 0x0A,

        /// <summary>
        /// Specialized use in conjunction with gateways, indicates that no response was obtained from the target device.
        /// </summary>
        GatewayTargetDeviceFailedToRespond = 0x0B,
    }
}
