
 /* This is automatically translated code. */

namespace FluentModbus
{
    public partial class ModbusRtuClient
    {
        ///<inheritdoc/>
        protected override async Task<Memory<byte>> TransceiveFrameAsync(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame, CancellationToken cancellationToken = default)
        {
            // WARNING: IF YOU EDIT THIS METHOD, REFLECT ALL CHANGES ALSO IN TransceiveFrameAsync!

            int frameLength;
            byte rawFunctionCode;
            ushort crc;

            // build request
            if (!(0 <= unitIdentifier && unitIdentifier <= 247))
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidUnitIdentifier);

            // special case: broadcast (only for write commands)
            if (unitIdentifier == 0)
            {
                switch (functionCode)
                {
                    case ModbusFunctionCode.WriteMultipleRegisters:
                    case ModbusFunctionCode.WriteSingleCoil:
                    case ModbusFunctionCode.WriteSingleRegister:
                    case ModbusFunctionCode.WriteMultipleCoils:
                    case ModbusFunctionCode.WriteFileRecord:
                    case ModbusFunctionCode.MaskWriteRegister:
                        break;
                    default:
                        throw new ModbusException(ErrorMessage.Modbus_InvalidUseOfBroadcast);
                }
            }

            _frameBuffer.Writer.Seek(0, SeekOrigin.Begin);
            _frameBuffer.Writer.Write(unitIdentifier);                                      // 00     Unit Identifier
            extendFrame(_frameBuffer.Writer);
            frameLength = (int)_frameBuffer.Writer.BaseStream.Position;

            // add CRC
            crc = ModbusUtils.CalculateCRC(_frameBuffer.Buffer.AsMemory()[..frameLength]);
            _frameBuffer.Writer.Write(crc);
            frameLength = (int)_frameBuffer.Writer.BaseStream.Position;

           LogFrame("Tx", _frameBuffer.Buffer.AsMemory(0, frameLength).ToArray());
            // send request
            await _serialPort!.Value.Value.WriteAsync(_frameBuffer.Buffer, 0, frameLength, cancellationToken).ConfigureAwait(false);

            // special case: broadcast (only for write commands)
            if (unitIdentifier == 0)
                return _frameBuffer.Buffer.AsMemory(0, 0);

            // wait for and process response
            frameLength = 0;
            _frameBuffer.Reader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                int numBytesRead = await _serialPort!.Value.Value.ReadAsync(_frameBuffer.Buffer, frameLength, _frameBuffer.Buffer.Length - frameLength, cancellationToken).ConfigureAwait(false);
                if (numBytesRead == 0)
                {
                    throw new IOException("Read resulted in 0 bytes returned.");
                }

                frameLength += numBytesRead;
                if (ModbusUtils.DetectResponseFrame(unitIdentifier, _frameBuffer.Buffer.AsMemory()[..frameLength]))
                {
                    LogFrame("Rx",_frameBuffer.Buffer.AsMemory()[..frameLength].ToArray());
                    break;
                }
                else
                {
                    // reset length because one or more chunks of data were received and written to
                    // the buffer, but no valid Modbus frame could be detected and now the buffer is full
                    if (frameLength == _frameBuffer.Buffer.Length)
                    {
                        frameLength = 0;
                    }
                }
            }

            _ = _frameBuffer.Reader.ReadByte();
            rawFunctionCode = _frameBuffer.Reader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                ProcessError(functionCode, (ModbusExceptionCode)_frameBuffer.Buffer[2]);

            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

            return _frameBuffer.Buffer.AsMemory(1, frameLength - 3);
        }    
    }
}