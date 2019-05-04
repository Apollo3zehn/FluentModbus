namespace ModbusTCP.NET
{
    public enum ModbusFunctionCode : byte
    {
        // class 0
        ReadHoldingRegisters = 0x03,        // FC03
        WriteMultipleRegisters = 0x10,      // FC16

        // class 1
        ReadCoils = 0x01,                   // FC01
        ReadDiscreteInputs = 0x02,          // FC02
        ReadInputRegisters = 0x04,          // FC04
        WriteSingleCoil = 0x05,             // FC05
        WriteSingleRegister = 0x06,         // FC06
        ReadExceptionStatus = 0x07,         // FC07 (Serial Line only)

        // class 2
        WriteMultipleCoils = 0x0F,          // FC15
        ReadFileRecord = 0x14,              // FC20
        WriteFileRecord = 0x15,             // FC21
        MaskWriteRegister = 0x16,           // FC22
        ReadWriteMultipleRegisters = 0x17,  // FC23
        ReadFifoQueue = 0x18,               // FC24

        //
        Error = 0x80                        // FC128
    }
}
