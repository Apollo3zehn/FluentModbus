using System;

namespace ModbusTCP.NET
{
    public class ModbusException : Exception
    {
        public ModbusException(string message) : base(message)
        {
            //
        }
    }
}
