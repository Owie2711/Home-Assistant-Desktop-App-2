using System.IO;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.Services;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;

    public FileLoggerProvider()
    {
        _dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomeAssistantDesktop", "Logs");
        Directory.CreateDirectory(_dir);
        RollIfNeeded();
    }

    private void RollIfNeeded()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var path = Path.Combine(_dir, $"ha-desktop-{today}.log");
        if (!File.Exists(path))
        {
            // keep at most 7 rolling logs
            var old = Directory.GetFiles(_dir, "ha-desktop-*.log")
                .OrderBy(f => File.GetLastWriteTimeUtc(f)).ToArray();
            for (var i = 0; i < old.Length - 6; i++)
            {
                try { File.Delete(old[i]); } catch { }
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_dir, categoryName);

    public void Dispose() { }
}

public sealed class FileLogger : ILogger
{
    private readonly string _dir;
    private readonly string _category;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private string? _currentDate;

    public FileLogger(string dir, string category)
    {
        _dir = dir;
        _category = category;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var msg = formatter(state, exception);
        var line = $"[{ts}] [{logLevel}] [{_category}] {msg}";
        if (exception is not null) line += $"{Environment.NewLine}{exception}";

        lock (_lock)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                if (_writer is null || _currentDate != today)
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                    _currentDate = today;
                    var path = Path.Combine(_dir, $"ha-desktop-{today}.log");
                    _writer = new StreamWriter(path, append: true) { AutoFlush = false };
                }
                _writer.WriteLine(line);
            }
            catch
            {
                // Silently ignore logging failures
            }
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            try { _writer?.Flush(); } catch { }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
