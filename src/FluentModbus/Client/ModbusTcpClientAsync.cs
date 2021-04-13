
 /* This is automatically translated code. */

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FluentModbus
{
	public partial class ModbusTcpClient
	{
		private protected override async Task<Memory<byte>> TransceiveFrameAsync(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame, CancellationToken cancellationToken = default)
        {
            int frameLength;
            int partialLength;

            ushort transactionIdentifier;
            ushort protocolIdentifier;
            ushort bytesFollowing;

            byte rawFunctionCode;

            bool isParsed;

            ModbusFrameBuffer frameBuffer;
            ExtendedBinaryWriter writer;
            ExtendedBinaryReader reader;

            bytesFollowing = 0;
            frameBuffer = _frameBuffer;
            writer = _frameBuffer.Writer;
            reader = _frameBuffer.Reader;

            // build request
            writer.Seek(7, SeekOrigin.Begin);
            extendFrame(writer);
            frameLength = (int)writer.BaseStream.Position;

            writer.Seek(0, SeekOrigin.Begin);

            if (BitConverter.IsLittleEndian)
            {
                writer.WriteReverse(this.GetTransactionIdentifier());          // 00-01  Transaction Identifier
                writer.WriteReverse((ushort)0);                                // 02-03  Protocol Identifier
                writer.WriteReverse((ushort)(frameLength - 6));                // 04-05  Length
            }
            else
            {
                writer.Write(this.GetTransactionIdentifier());                 // 00-01  Transaction Identifier
                writer.Write((ushort)0);                                       // 02-03  Protocol Identifier
                writer.Write((ushort)(frameLength - 6));                       // 04-05  Length
            }
            
            writer.Write(unitIdentifier);                                      // 06     Unit Identifier

            // send request
            await _networkStream.WriteAsync(frameBuffer.Buffer, 0, frameLength, cancellationToken).ConfigureAwait(false);

            // wait for and process response
            frameLength = 0;
            isParsed = false;
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                partialLength = await _networkStream.ReadAsync(frameBuffer.Buffer, frameLength, frameBuffer.Buffer.Length - frameLength, cancellationToken).ConfigureAwait(false);

                /* From MSDN (https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.read):
                 * Implementations of this method read a maximum of count bytes from the current stream and store 
                 * them in buffer beginning at offset. The current position within the stream is advanced by the 
                 * number of bytes read; however, if an exception occurs, the current position within the stream 
                 * remains unchanged. Implementations return the number of bytes read. The implementation will block 
                 * until at least one byte of data can be read, in the event that no data is available. Read returns
                 * 0 only when there is no more data in the stream and no more is expected (such as a closed socket or end of file).
                 * An implementation is free to return fewer bytes than requested even if the end of the stream has not been reached.
                 */
                if (partialLength == 0)
                    throw new InvalidOperationException(ErrorMessage.ModbusClient_TcpConnectionClosedUnexpectedly);

                frameLength += partialLength;

                if (frameLength >= 7)
                {
                    if (!isParsed) // read MBAP header only once
                    {
                        // read MBAP header
                        transactionIdentifier = reader.ReadUInt16Reverse();              // 00-01  Transaction Identifier
                        protocolIdentifier = reader.ReadUInt16Reverse();                 // 02-03  Protocol Identifier               
                        bytesFollowing = reader.ReadUInt16Reverse();                     // 04-05  Length
                        unitIdentifier = reader.ReadByte();                              // 06     Unit Identifier

                        if (protocolIdentifier != 0)
                            throw new ModbusException(ErrorMessage.ModbusClient_InvalidProtocolIdentifier);

                        isParsed = true;
                    }

                    // full frame received
                    if (frameLength - 6 >= bytesFollowing)
                        break;
                }
            }

            rawFunctionCode = reader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                this.ProcessError(functionCode, (ModbusExceptionCode)frameBuffer.Buffer[8]);

            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

            return frameBuffer.Buffer.AsMemory(7, frameLength - 7);
        }	
	}
}