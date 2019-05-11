using System;

namespace FluentModbus
{
    public class ModbusException : Exception
    {
        public ModbusException(string message) : base(message)
        {
            //
        }
    }
}
