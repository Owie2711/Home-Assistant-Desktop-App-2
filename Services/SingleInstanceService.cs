using System.Threading;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsFirstInstance { get; }

    public SingleInstanceService(ILogger log)
    {
        var guid = "HomeAssistantDesktop-Singleton-9f3a2c1b";
        _mutex = new Mutex(true, guid, out var createdNew);
        IsFirstInstance = createdNew;
        log.LogInformation("Single instance check: first={First}", IsFirstInstance);
    }

    public void Dispose()
    {
        if (IsFirstInstance)
        {
            try { _mutex.ReleaseMutex(); } catch { }
        }
        _mutex.Dispose();
    }
}
