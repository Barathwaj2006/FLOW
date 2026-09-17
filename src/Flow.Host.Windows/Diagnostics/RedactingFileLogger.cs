using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Flow.Host.Windows.Diagnostics;

/// <summary>
/// Production rolling file logger provider that automatically sanitizes all log messages
/// with PiiDataScrubber before writing to disk.
/// </summary>
public sealed class RedactingFileLoggerProvider : ILoggerProvider, IAsyncDisposable, IDisposable
{
    private readonly string _logsDirectory;
    private readonly LogLevel _minimumLevel;
    private readonly int _retentionDays;
    private readonly long _maxFileSize;
    private readonly BlockingCollection<string> _messageQueue = new(new ConcurrentQueue<string>(), 5000);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;
    private bool _isDisposed;

    public string LogsDirectory => _logsDirectory;

    public RedactingFileLoggerProvider(
        string? logsDirectory = null,
        LogLevel minimumLevel = LogLevel.Information,
        int retentionDays = 7,
        long maxFileSize = 10 * 1024 * 1024) // 10 MB
    {
        _minimumLevel = minimumLevel;
        _retentionDays = retentionDays;
        _maxFileSize = maxFileSize;

        _logsDirectory = string.IsNullOrWhiteSpace(logsDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOW", "logs")
            : logsDirectory;

        try
        {
            Directory.CreateDirectory(_logsDirectory);
            PruneOldLogs();
        }
        catch { }

        _workerTask = Task.Run(ProcessLogQueueAsync);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new RedactingFileLogger(categoryName, this, _minimumLevel);
    }

    internal void EnqueueLog(string message)
    {
        if (_isDisposed || _messageQueue.IsAddingCompleted) return;
        try
        {
            _messageQueue.TryAdd(message);
        }
        catch { }
    }

    private async Task ProcessLogQueueAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (_messageQueue.TryTake(out string? entry, 250, _cts.Token))
                {
                    await WriteLogToFileAsync(entry);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch { }
        }

        // Flush remaining queue
        while (_messageQueue.TryTake(out string? entry))
        {
            try
            {
                await WriteLogToFileAsync(entry);
            }
            catch { }
        }
    }

    private async Task WriteLogToFileAsync(string entry)
    {
        try
        {
            Directory.CreateDirectory(_logsDirectory);
            string dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            string logFilePath = Path.Combine(_logsDirectory, $"flow-{dateStr}.log");

            // Enforce max file size
            if (File.Exists(logFilePath))
            {
                var fileInfo = new FileInfo(logFilePath);
                if (fileInfo.Length > _maxFileSize)
                {
                    string rolledName = Path.Combine(_logsDirectory, $"flow-{dateStr}-{DateTime.UtcNow:HHmmss}.log");
                    File.Move(logFilePath, rolledName);
                }
            }

            byte[] bytes = Encoding.UTF8.GetBytes(entry + Environment.NewLine);
            await using var stream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, useAsync: true);
            await stream.WriteAsync(bytes);
        }
        catch { }
    }

    public void PruneOldLogs()
    {
        try
        {
            if (!Directory.Exists(_logsDirectory)) return;

            var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
            var files = Directory.GetFiles(_logsDirectory, "flow-*.log");
            foreach (var file in files)
            {
                var info = new FileInfo(file);
                if (info.CreationTimeUtc < cutoff && info.LastWriteTimeUtc < cutoff)
                {
                    try { info.Delete(); } catch { }
                }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _messageQueue.CompleteAdding();
            _cts.Cancel();
            _workerTask.Wait(TimeSpan.FromSeconds(2));
            _cts.Dispose();
            _messageQueue.Dispose();
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _messageQueue.CompleteAdding();
            _cts.Cancel();
            await _workerTask.ConfigureAwait(false);
            _cts.Dispose();
            _messageQueue.Dispose();
        }
        catch { }
    }
}

/// <summary>
/// Individual logger instance category that scrubs messages before queueing.
/// </summary>
public sealed class RedactingFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly RedactingFileLoggerProvider _provider;
    private readonly LogLevel _minimumLevel;

    public RedactingFileLogger(string categoryName, RedactingFileLoggerProvider provider, LogLevel minimumLevel)
    {
        _categoryName = categoryName;
        _provider = provider;
        _minimumLevel = minimumLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel && logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        string message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null) return;

        string fullMessage = exception != null
            ? $"{message} Exception: {exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}"
            : message;

        string scrubbed = PiiDataScrubber.Scrub(fullMessage);
        string levelAbbr = GetLevelAbbreviation(logLevel);
        string logLine = $"[{DateTime.UtcNow:O}] [{levelAbbr}] [{_categoryName}] [T{Environment.CurrentManagedThreadId}] {scrubbed}";

        _provider.EnqueueLog(logLine);
    }

    private static string GetLevelAbbreviation(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "INF"
    };
}
