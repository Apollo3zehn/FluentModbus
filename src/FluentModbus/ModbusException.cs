using System;

namespace FluentModbus
{
    /// <summary>
    /// This exception is used for Modbus protocol errors.
    /// </summary>
    public class ModbusException : Exception
    {
        internal ModbusException(string message) : base(message)
        {
            //
        }
    }
}
