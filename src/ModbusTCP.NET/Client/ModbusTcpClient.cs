using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace ModbusTCP.NET
{
    public class ModbusTcpClient
    {
        ushort _transactionIdentifierBase;
        object _transactionIdentifierLock;

        TcpClient _tcpClient;
        NetworkStream _networkStream;

        public ModbusTcpClient()
        {
            _transactionIdentifierBase = 0;
            _transactionIdentifierLock = new object();
        }

        public bool IsConnected
        {
            get
            {
                return _tcpClient != null ? _tcpClient.Connected : false;
            }
        }

        public void Connect()
        {
            this.Connect(new IPEndPoint(IPAddress.Loopback, 502));
        }

        public void Connect(IPAddress remoteIpAddress)
        {
            this.Connect(new IPEndPoint(remoteIpAddress, 502));
        }

        public void Connect(IPEndPoint remoteEndpoint)
        {
            _tcpClient?.Close();
            _tcpClient = new TcpClient();

            if (!_tcpClient.ConnectAsync(remoteEndpoint.Address, remoteEndpoint.Port).Wait(1000))
            {
                throw new Exception(ErrorMessage.ModbusClient_TcpConnectionTimeout);
            }

            _networkStream = _tcpClient.GetStream();
            _networkStream.ReadTimeout = 1000;
        }

        public void Disconnect()
        {
            _tcpClient?.Close();
        }

        public static void Test()
        {
            byte unitIdentifier;

            ModbusTcpClient modbusClient;
            ModbusTcpMessageBuffer modbusTcpMessageBuffer;
            Span<byte> data;

            unitIdentifier = 0xFF;

            modbusClient = new ModbusTcpClient();
            modbusClient.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 502));

            modbusTcpMessageBuffer = new ModbusTcpMessageBuffer();

            try
            {
                // ReadHoldingRegisters = 0x03,        // FC03
                data = modbusClient.ReadHoldingRegisters(unitIdentifier, 0, 10, modbusTcpMessageBuffer);

                // WriteMultipleRegisters = 0x10,      // FC16
                modbusClient.WriteMultipleRegisters(unitIdentifier, 0, new byte[] { 10, 00, 20, 00, 30, 00, 255, 00, 255, 01 }, modbusTcpMessageBuffer);

                // ReadCoils = 0x01,                   // FC01
                data = modbusClient.ReadCoils(unitIdentifier, 0, 10, modbusTcpMessageBuffer);

                // ReadDiscreteInputs = 0x02,          // FC02
                data = modbusClient.ReadDiscreteInputs(unitIdentifier, 0, 10, modbusTcpMessageBuffer);

                // ReadInputRegisters = 0x04,          // FC04
                data = modbusClient.ReadInputRegisters(unitIdentifier, 0, 10, modbusTcpMessageBuffer);

                // WriteSingleCoil = 0x05,             // FC05
                modbusClient.WriteSingleCoil(unitIdentifier, 0, true, modbusTcpMessageBuffer);

                // WriteSingleRegister = 0x06,         // FC06
                modbusClient.WriteSingleRegister(unitIdentifier, 0, new byte[] { 65, 67 }, modbusTcpMessageBuffer);
            }
            finally
            {
                modbusTcpMessageBuffer.Dispose();
            }
        }

        private UInt16 GetTransactionIdentifier()
        {
            lock (_transactionIdentifierLock)
            {
                return _transactionIdentifierBase++;
            }
        }

        private Span<byte> TransceiveFrame(byte unitIdentifier, ModbusFunctionCode modbusFunctionCode, ModbusTcpMessageBuffer messageBuffer, Action<ExtendedBinaryWriter> extendFrame)
        {
            int length;

            ushort transactionIdentifier;
            ushort protocolIdentifier;
            ushort bytesFollowing;

            byte rawFunctionCode;

            bytesFollowing = 0;

            // build and send request
            messageBuffer.RequestWriter.Seek(7, SeekOrigin.Begin);
            extendFrame.Invoke(messageBuffer.RequestWriter);
            length = (int)messageBuffer.RequestWriter.BaseStream.Position;

            messageBuffer.RequestWriter.Seek(0, SeekOrigin.Begin);
            messageBuffer.RequestWriter.WriteReverse(this.GetTransactionIdentifier());              // 00-01  Transaction Identifier
            messageBuffer.RequestWriter.WriteReverse((ushort)0);                                    // 02-03  Protocol Identifier
            messageBuffer.RequestWriter.WriteReverse((ushort)(length - 6));                         // 04-05  Length
            messageBuffer.RequestWriter.Write(unitIdentifier);                                      // 06     Unit Identifier

            _networkStream.Write(messageBuffer.Buffer, 0, length);

            // wait for and process response
            length = 0;
            messageBuffer.ResponseReader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                length += _networkStream.Read(messageBuffer.Buffer, 0, messageBuffer.Buffer.Length);

                if (length >= 7)
                {
                    if (messageBuffer.ResponseReader.BaseStream.Position == 0)
                    {
                        // read MBAP header
                        transactionIdentifier = messageBuffer.ResponseReader.ReadUInt16Reverse();              // 00-01  Transaction Identifier
                        protocolIdentifier = messageBuffer.ResponseReader.ReadUInt16Reverse();                 // 02-03  Protocol Identifier               
                        bytesFollowing = messageBuffer.ResponseReader.ReadUInt16Reverse();                     // 04-05  Length
                        unitIdentifier = messageBuffer.ResponseReader.ReadByte();                              // 06     Unit Identifier

                        if (protocolIdentifier != 0)
                        {
                            throw new ModbusException(ErrorMessage.ModbusClient_ProtocolIdentifierInvalid);
                        }
                    }

                    if (length - 6 >= bytesFollowing)
                    {
                        break;
                    }
                }
            }

            rawFunctionCode = messageBuffer.ResponseReader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)modbusFunctionCode)
            {
                switch ((ModbusExceptionCode)messageBuffer.Buffer[8])
                {
                    case ModbusExceptionCode.IllegalFunction:
                        throw new ModbusException(ErrorMessage.ModbusClient_0x01_IllegalFunction);
                    case ModbusExceptionCode.IllegalDataAddress:
                        throw new ModbusException(ErrorMessage.ModbusClient_0x02_IllegalDataAddress);
                    case ModbusExceptionCode.IllegalDataValue:

                        switch (modbusFunctionCode)
                        {
                            case ModbusFunctionCode.WriteMultipleRegisters:
                                throw new ModbusException(ErrorMessage.ModbusClient_0x03_IllegalDataValue_0x7B);

                            case ModbusFunctionCode.ReadHoldingRegisters:
                            case ModbusFunctionCode.ReadInputRegisters:
                                throw new ModbusException(ErrorMessage.ModbusClient_0x03_IllegalDataValue_0x7D);

                            case ModbusFunctionCode.ReadCoils:
                            case ModbusFunctionCode.ReadDiscreteInputs:
                                throw new ModbusException(ErrorMessage.ModbusClient_0x03_IllegalDataValue_0x7D0);

                            default:
                                throw new ModbusException(ErrorMessage.ModbusClient_0x03_IllegalDataValue);
                        }

                    case ModbusExceptionCode.ServerDeviceFailure:
                        throw new ModbusException(ErrorMessage.ModbusClient_0x04_ServerDeviceFailure);
                    case ModbusExceptionCode.Acknowledge:
                        throw new ModbusException(ErrorMessage.ModbusClient_0x05_Acknowledge);
                    case ModbusExceptionCode.ServerDeviceBusy:
                        throw new ModbusException(ErrorMessage.ModbusClient_0x06_ServerDeviceBusy);
                    case ModbusExceptionCode.MemoryParityError:
                        throw new ModbusException(ErrorMessage.ModbusClient_0x08_MemoryParityError);
                    case ModbusExceptionCode.GatewayPathUnavailable:
                        throw new ModbusException(ErrorMessage.ModbusClient_0x0A_GatewayPathUnavailable);
                    case ModbusExceptionCode.GatewayTargetDeviceFailedToRespond:
                        throw new ModbusException(ErrorMessage.ModbusClient_0x0B_GatewayTargetDeviceFailedToRespond);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else if (rawFunctionCode != (byte)modbusFunctionCode)
            {
                throw new ModbusException(ErrorMessage.ModbusClient_ResponseFunctionCodeInvalid);
            }

            return messageBuffer.Buffer.AsSpan(7, length - 7);
        }

        // class 0
        public Span<byte> ReadHoldingRegisters(byte unitIdentifier, ushort startingAddress, ushort quantity, ModbusTcpMessageBuffer messageBuffer)
        {
            Span<byte> buffer;

            buffer = this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.ReadHoldingRegisters, messageBuffer, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.ReadHoldingRegisters);              // 07     Function Code
                requestWriter.WriteReverse(startingAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse(quantity);                                            // 10-11  Quantity of Input Registers
            }).Slice(2);

            if (buffer.Length < quantity * 2)
            {
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);
            }

            return buffer;
        }

        public void WriteMultipleRegisters(byte unitIdentifier, ushort startingAddress, byte[] dataset, ModbusTcpMessageBuffer messageBuffer)
        {
            int quantity;

            quantity = dataset.Length / 2;

            this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.WriteMultipleRegisters, messageBuffer, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.WriteMultipleRegisters);            // 07     Function Code
                requestWriter.WriteReverse(startingAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse((ushort)quantity);                                    // 10-11  Quantity of Registers
                requestWriter.Write((byte)(quantity * 2));                                       // 12     Byte Count = Quantity of Registers * 2

                requestWriter.Write(dataset, 0, dataset.Length);
            });
        }

        // class 1
        public Span<byte> ReadCoils(byte unitIdentifier, ushort startingAddress, ushort quantity, ModbusTcpMessageBuffer messageBuffer)
        {
            Span<byte> buffer;

            buffer = this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.ReadCoils, messageBuffer, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.ReadCoils);                         // 07     Function Code
                requestWriter.WriteReverse(startingAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse(quantity);                                            // 10-11  Quantity of Coils
            }).Slice(2);

            if (buffer.Length < (byte)Math.Ceiling((double)quantity / 8))
            {
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);
            }

            return buffer;
        }

        public Span<byte> ReadDiscreteInputs(byte unitIdentifier, ushort startingAddress, ushort quantity, ModbusTcpMessageBuffer messageBuffer)
        {
            Span<byte> buffer;

            buffer = this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.ReadDiscreteInputs, messageBuffer, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.ReadDiscreteInputs);                // 07     Function Code
                requestWriter.WriteReverse(startingAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse(quantity);                                            // 10-11  Quantity of Coils
            }).Slice(2);

            if (buffer.Length < (byte)Math.Ceiling((double)quantity / 8))
            {
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);
            }

            return buffer;
        }

        public Span<byte> ReadInputRegisters(byte unitIdentifier, ushort startingAddress, ushort quantity, ModbusTcpMessageBuffer messageBuffer)
        {
            Span<byte> buffer;

            buffer = this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.ReadInputRegisters, messageBuffer, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.ReadInputRegisters);                // 07     Function Code
                requestWriter.WriteReverse(startingAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse(quantity);                                            // 10-11  Quantity of Input Registers
            }).Slice(2);

            if (buffer.Length < quantity * 2)
            {
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseMessageLength);
            }

            return buffer;
        }

        public void WriteSingleCoil(byte unitIdentifier, ushort registerAddress, bool value, ModbusTcpMessageBuffer messageBuffer)
        {
            this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.WriteSingleCoil, messageBuffer, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.WriteSingleCoil);                   // 07     Function Code
                requestWriter.WriteReverse(registerAddress);                                     // 08-09  Starting Address
                requestWriter.WriteReverse((ushort)(value ? 0xFF00 : 0x0000));                   // 10-11  Value
            });
        }

        public void WriteSingleRegister(byte unitIdentifier, ushort registerAddress, byte[] value, ModbusTcpMessageBuffer messageBuffer)
        {
            Contract.Requires(value.Length == 2);

            this.TransceiveFrame(unitIdentifier, ModbusFunctionCode.WriteSingleRegister, messageBuffer, requestWriter =>
            {
                requestWriter.Write((byte)ModbusFunctionCode.WriteSingleRegister);               // 07     Function Code
                requestWriter.WriteReverse(registerAddress);                                     // 08-09  Starting Address
                requestWriter.Write(value);                                                      // 10-11  Value
            });
        }

        // class 2
        [Obsolete("This method is not implemented.")]
        public void WriteMultipleCoils()
        {
            throw new NotImplementedException();
        }

        [Obsolete("This method is not implemented.")]
        public void ReadFileRecord()
        {
            throw new NotImplementedException();
        }

        [Obsolete("This method is not implemented.")]
        public void WriteFileRecord()
        {
            throw new NotImplementedException();
        }

        [Obsolete("This method is not implemented.")]
        public void MaskWriteRegister()
        {
            throw new NotImplementedException();
        }

        [Obsolete("This method is not implemented.")]
        public void ReadWriteMultipleRegisters()
        {
            throw new NotImplementedException();
        }

        [Obsolete("This method is not implemented.")]
        public void ReadFifoQueue()
        {
            throw new NotImplementedException();
        }
    }
}
