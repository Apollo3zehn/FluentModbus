using System.Diagnostics;

namespace FluentModbus;
public interface IModbusRequestHandler : IDisposable
{
    CancellationToken CancellationToken { get; }
    string DisplayName { get; }
    bool IsReady { get; }
    Stopwatch LastRequest { get; }
    int Length { get; }
    ModbusServer ModbusServer { get; }

    void CancelToken();
    void OnResponseReady(int frameLength);
    Task ReceiveRequestAsync();
    void WriteResponse();
}