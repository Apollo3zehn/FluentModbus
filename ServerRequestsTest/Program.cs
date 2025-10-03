using FluentModbus;
using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;

namespace ServerRequestsTest;

internal class Program
{
  static void Main(string[] args)
  {
    try
    {
      ModbusTcpServer server = new ModbusTcpServer();
      server.AddUnit(1);
      Span<short> register = server.GetHoldingRegisters(1);
      UInt32 value = 327680;
      register.SetLittleEndianSwapped<UInt32>(0, value);
      ushort getValue = register.GetLittleEndianSwapped<ushort>(1);
      Console.WriteLine($"Get: {getValue}");
      server.Start(IPAddress.Parse("192.168.25.201"));
      Console.ReadKey();
    }
    catch (Exception ex)
    {
      Console.WriteLine(ex.StackTrace);
    }
  }
}
