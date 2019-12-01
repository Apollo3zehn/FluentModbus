﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace FluentModbus
{
    /// <summary>
    /// A Modbus TCP client.
    /// </summary>
    public class ModbusTcpClient : ModbusClient
    {
        #region Fields

        private ushort _transactionIdentifierBase;
        private object _transactionIdentifierLock;

        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private ModbusFrameBuffer _frameBuffer;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new Modbus TCP client for communication with Modbus TCP servers or bridges, routers and gateways for communication with serial line end units.
        /// </summary>
        public ModbusTcpClient()
        {
            _transactionIdentifierBase = 0;
            _transactionIdentifierLock = new object();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets or sets the connect timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int ConnectTimeout { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the read timeout in milliseconds. Default is 1000 ms.
        /// </summary>
        public int ReadTimeout { get; set; } = 1000;

        /// <summary>
        /// Gets the connection status of the underlying TCP client.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return _tcpClient != null ? _tcpClient.Connected : false;
            }
        }

        /// <summary>
        /// Connect to localhost at port 502. 
        /// </summary>
        public void Connect()
        {
            this.Connect(new IPEndPoint(IPAddress.Loopback, 502));
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteIpAddress"/> at port 502.
        /// </summary>
        /// <param name="remoteIpAddress">The IP address of the end unit. Example: IPAddress.Parse("192.168.0.1").</param>
        public void Connect(IPAddress remoteIpAddress)
        {
            this.Connect(new IPEndPoint(remoteIpAddress, 502));
        }

        /// <summary>
        /// Connect to the specified <paramref name="remoteEndpoint"/>.
        /// </summary>
        /// <param name="remoteEndpoint">The IP address and port of the end unit.</param>
        public void Connect(IPEndPoint remoteEndpoint)
        {
            _frameBuffer = new ModbusFrameBuffer(260);

            _tcpClient?.Close();
            _tcpClient = new TcpClient();

            if (!_tcpClient.ConnectAsync(remoteEndpoint.Address, remoteEndpoint.Port).Wait(this.ConnectTimeout))
            {
                throw new Exception(ErrorMessage.ModbusClient_TcpConnectionTimeout);
            }

            _networkStream = _tcpClient.GetStream();
            _networkStream.ReadTimeout = this.ReadTimeout;
        }

        /// <summary>
        /// Disconnect from the end unit.
        /// </summary>
        public void Disconnect()
        {
            _tcpClient?.Close();
            _frameBuffer?.Dispose();
        }

        internal protected override Span<byte> TransceiveFrame(byte unitIdentifier, ModbusFunctionCode functionCode, Action<ExtendedBinaryWriter> extendFrame)
        {
            int frameLength;
            int partialLength;

            ushort transactionIdentifier;
            ushort protocolIdentifier;
            ushort bytesFollowing;

            byte rawFunctionCode;

            ModbusFrameBuffer frameBuffer;
            ExtendedBinaryWriter requestWriter;
            ExtendedBinaryReader responseReader;

            bytesFollowing = 0;
            frameBuffer = _frameBuffer;
            requestWriter = _frameBuffer.RequestWriter;
            responseReader = _frameBuffer.ResponseReader;

            // build request
            requestWriter.Seek(7, SeekOrigin.Begin);
            extendFrame.Invoke(requestWriter);
            frameLength = (int)requestWriter.BaseStream.Position;

            requestWriter.Seek(0, SeekOrigin.Begin);
            requestWriter.WriteReverse(this.GetTransactionIdentifier());              // 00-01  Transaction Identifier
            requestWriter.WriteReverse((ushort)0);                                    // 02-03  Protocol Identifier
            requestWriter.WriteReverse((ushort)(frameLength - 6));                    // 04-05  Length
            requestWriter.Write(unitIdentifier);                                      // 06     Unit Identifier

            // send request
            _networkStream.Write(frameBuffer.Buffer, 0, frameLength);

            // wait for and process response
            frameLength = 0;
            responseReader.BaseStream.Seek(0, SeekOrigin.Begin);

            while (true)
            {
                partialLength = _networkStream.Read(frameBuffer.Buffer, frameLength, frameBuffer.Buffer.Length - frameLength);

                if (partialLength == 0)
                    throw new InvalidOperationException(ErrorMessage.ModbusClient_TcpConnectionClosedUnexpectedly);

                frameLength += partialLength;

                if (frameLength >= 7)
                {
                    if (responseReader.BaseStream.Position == 0) // read MBAP header only once
                    {
                        // read MBAP header
                        transactionIdentifier = responseReader.ReadUInt16Reverse();              // 00-01  Transaction Identifier
                        protocolIdentifier = responseReader.ReadUInt16Reverse();                 // 02-03  Protocol Identifier               
                        bytesFollowing = responseReader.ReadUInt16Reverse();                     // 04-05  Length
                        unitIdentifier = responseReader.ReadByte();                              // 06     Unit Identifier

                        if (protocolIdentifier != 0)
                        {
                            throw new ModbusException(ErrorMessage.ModbusClient_InvalidProtocolIdentifier);
                        }
                    }

                    if (frameLength - 6 >= bytesFollowing)
                        break;
                }
            }

            rawFunctionCode = responseReader.ReadByte();

            if (rawFunctionCode == (byte)ModbusFunctionCode.Error + (byte)functionCode)
                this.ProcessError(functionCode, (ModbusExceptionCode)frameBuffer.Buffer[8]);
            else if (rawFunctionCode != (byte)functionCode)
                throw new ModbusException(ErrorMessage.ModbusClient_InvalidResponseFunctionCode);

            return frameBuffer.Buffer.AsSpan(7, frameLength - 7);
        }

        private ushort GetTransactionIdentifier()
        {
            lock (_transactionIdentifierLock)
            {
                return _transactionIdentifierBase++;
            }
        }

        #endregion
    }
}
