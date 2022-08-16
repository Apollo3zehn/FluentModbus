namespace FluentModbus
{
    /// <summary>
    /// This exception is used for Modbus protocol errors.
    /// </summary>
    public class ModbusException : Exception
    {
        internal ModbusException(string message) : base(message)
        {
            ExceptionCode -= 1;
        }

        internal ModbusException(ModbusExceptionCode exceptionCode, string message) : base(message)
        {
            ExceptionCode = exceptionCode;
        }

        /// <summary>
        /// The Modbus exception code. A value of -1 indicates that there is no specific exception code.
        /// </summary>
        public ModbusExceptionCode ExceptionCode { get; }
    }
}
