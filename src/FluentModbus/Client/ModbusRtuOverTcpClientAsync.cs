





 /* This is automatically translated code. */
 
namespace FluentModbus
{
    public partial class ModbusRtuOverTcpClient
    {
        ///<inheritdoc/>
        protected override async Task<Memory<byte>> TransceiveFrameAsync(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame, CancellationToken cancellationToken = default)
        {
            // WARNING: IF YOU EDIT THIS METHOD, REFLECT ALL CHANGES ALSO IN TransceiveFrameAsync!

            int frameLength;
            byte rawFunctionCode;

            ModbusFrameBuffer frameBuffer;
            ExtendedBinaryWriter writer;
            ExtendedBinaryReader reader;

            frameBuffer = _frameBuffer;
            writer = _frameBuffer.Writer;
            reader = _frameBuffer.Reader;

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

            writer.Seek(0, SeekOrigin.Begin);
            writer.Write(unitIdentifier);                                      // 00     Unit Identifier
            extendFrame(writer);
            frameLength = (int)writer.BaseStream.Position;

            // add CRC
            crc = ModbusUtils.CalculateCRC(frameBuffer.Buffer.AsMemory()[..frameLength]);
            writer.Write(crc);
            frameLength = (int)writer.BaseStream.Position;

            // send request
            await _networkStream.WriteAsync(frameBuffer.Buffer, 0, frameLength, cancellationToken).ConfigureAwait(false);

            // special case: broadcast (only for write commands)
            if (unitIdentifier == 0)
                return _frameBuffer.Buffer.AsMemory(0, 0);

            // wait for and process response
            frameLength = 0;
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                using var timeoutCts = new CancellationTokenSource(_networkStream.ReadTimeout);
                
                // https://stackoverflow.com/a/62162138
                // https://github.com/Apollo3zehn/FluentModbus/blob/181586d88cbbef3b2b3e6ace7b29099e04b30627/src/FluentModbus/ModbusRtuSerialPort.cs#L54
                using (timeoutCts.Token.Register(_networkStream.Close))
                using (cancellationToken.Register(timeoutCts.Cancel))
                {
                    try
                    {
                        frameLength += await _networkStream.ReadAsync(frameBuffer.Buffer, frameLength, frameBuffer.Buffer.Length - frameLength, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                    {
                        throw new TimeoutException("The asynchronous read operation timed out.");
                    }
                    catch (IOException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException("The asynchronous read operation timed out.");
                    }
                }

                /* From MSDN (https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.read):
                 * Implementations of this method read a maximum of count bytes from the current stream and store 
                 * them in buffer beginning at offset. The current position within the stream is advanced by the 
                 * number of bytes read; however, if an exception occurs, the current position within the stream 
                 * remains unchanged. Implementations return the number of bytes read. The implementation will block 
                 * until at least one byte of data can be read, in the event that no data is available. Read returns
                 * 0 only when there is no more data in the stream and no more is expected (such as a closed socket or end of file).
                 * An implementation is free to return fewer bytes than requested even if the end of the stream has not been reached.
                 */
                
                if (ModbusUtils.DetectResponseFrame(unitIdentifier, _frameBuffer.Buffer.AsMemory()[..frameLength]))
                {
                    break;
                }
                else
                {
                    // reset length because one or more chunks of data were received and written to
                    // the buffer, but no valid Modbus frame could be detected and now the buffer is full
                    if (frameLength == _frameBuffer.Buffer.Length)
                        frameLength = 0;
                }
            }

            _ = reader.ReadByte();
            rawFunctionCode = reader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                ProcessError(functionCode, (ModbusExceptionCode)_frameBuffer.Buffer[2]);

            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

            return _frameBuffer.Buffer.AsMemory(1, frameLength - 3);
        }
    
    }
}