namespace FluentModbus
{
    /// <summary>
    /// Specifies the action the Modbus server is requested to do.
    /// </summary>
    public enum ModbusFunctionCode : byte
    {
        // class 0

        /// <summary>
        /// This function code is used to read the contents of a contiguous block of holding registers in a remote device.
        /// </summary>
        ReadHoldingRegisters = 0x03,        // FC03

        /// <summary>
        /// This function code is used to write a block of contiguous registers (1 to 123 registers) in a remote device. 
        /// </summary>
        WriteMultipleRegisters = 0x10,      // FC16

        // class 1
        /// <summary>
        /// This function code is used to read from 1 to 2000 contiguous status of coils in a remote device.
        /// </summary>
        ReadCoils = 0x01,                   // FC01

        /// <summary>
        /// This function code is used to read from 1 to 2000 contiguous status of discrete inputs in a remote device.
        /// </summary>
        ReadDiscreteInputs = 0x02,          // FC02

        /// <summary>
        /// This function code is used to read from 1 to 125 contiguous input registers in a remote device.
        /// </summary>
        ReadInputRegisters = 0x04,          // FC04

        /// <summary>
        /// This function code is used to write a single output to either ON or OFF in a remote device.
        /// </summary>
        WriteSingleCoil = 0x05,             // FC05

        /// <summary>
        /// This function code is used to write a single holding register in a remote device.
        /// </summary>
        WriteSingleRegister = 0x06,         // FC06

        /// <summary>
        /// This function code is used to read the contents of eight Exception  Status outputs in a remote device.
        /// </summary>
        ReadExceptionStatus = 0x07,         // FC07 (Serial Line only)

        // class 2

        /// <summary>
        /// This function code is used to force each coil in a sequence of coils to either ON or OFF in a remote device.
        /// </summary>
        WriteMultipleCoils = 0x0F,          // FC15

        /// <summary>
        /// This function code is used to perform a file record read.
        /// </summary>
        ReadFileRecord = 0x14,              // FC20

        /// <summary>
        /// This function code is used to perform a file record write.
        /// </summary>
        WriteFileRecord = 0x15,             // FC21

        /// <summary>
        /// This function code is used to modify the contents of a specified holding register using a combination of an AND mask, an OR mask, and the register's current contents.
        /// </summary>
        MaskWriteRegister = 0x16,           // FC22

        /// <summary>
        /// This function code performs a combination of one read operation and one write operation in a single MODBUS transaction. The write operation is performed before the read.
        /// </summary>
        ReadWriteMultipleRegisters = 0x17,  // FC23

        /// <summary>
        /// This function code allows to read the contents of a First-In-First-Out (FIFO) queue of register in a remote device.
        /// </summary>
        ReadFifoQueue = 0x18,               // FC24

        //
        /// <summary>
        /// This function code is added to another function code to indicate that an error occured.
        /// </summary>
        Error = 0x80                        // FC128
    }
}
