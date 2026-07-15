using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Windows.Storage;

namespace JellyBox.Services;

/// <summary>
/// An <see cref="ILoggerProvider"/> that writes log entries to a rotating file under the app's
/// local data folder. This persists logs on a sideloaded UWP/Xbox install where console and debug
/// output is unavailable; the files can be retrieved from a dev console via Device Portal
/// (LocalState\logs).
/// </summary>
/// <remarks>
/// Entries are handed to a single background writer thread. Enqueuing is non-blocking: entries are
/// dropped rather than stalling app threads when the bounded queue is full. <see cref="Flush"/>
/// provides a bounded synchronous drain for crash and suspend paths, where losing the most recent
/// entries would defeat the purpose of logging them.
/// </remarks>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed class FileLoggerProvider : ILoggerProvider
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private const long MaxLogFileSizeBytes = 4L * 1024 * 1024;
    private const int MaxRetainedFiles = 3;
    private const int MaxQueuedEntries = 1024;

    private readonly string _logDirectory;
    private readonly string _logFilePath;
    private readonly BlockingCollection<LogItem> _queue = new(MaxQueuedEntries);
    private readonly Thread _writerThread;
    private bool _disposed;

    public FileLoggerProvider()
    {
        _logDirectory = Path.Combine(ApplicationData.Current.LocalFolder.Path, "logs");
        _logFilePath = Path.Combine(_logDirectory, "jellybox.log");

        _writerThread = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "FileLoggerWriter",
        };
        _writerThread.Start();
    }

    public ILogger CreateLogger(string categoryName)
        => new FileLogger(categoryName, line => Enqueue(new LogItem(line, null)));

    /// <summary>
    /// Blocks until entries enqueued before this call are written to disk, or until the timeout
    /// elapses. Intended for crash and suspend handlers so the latest entries are persisted.
    /// </summary>
    public void Flush(TimeSpan timeout)
    {
        if (!_writerThread.IsAlive || _queue.IsAddingCompleted)
        {
            return;
        }

        long startMilliseconds = Environment.TickCount64;
        using ManualResetEventSlim signal = new(initialState: false);

        // Enqueue the flush marker with a bounded wait so a momentarily full queue can't cause
        // Flush to no-op and drop the very entries a crash/suspend needs persisted. The writer is
        // actively draining, so a slot frees up quickly.
        if (!TryAddWithTimeout(new LogItem(null, signal), timeout))
        {
            return;
        }

        TimeSpan remaining = timeout - TimeSpan.FromMilliseconds(Environment.TickCount64 - startMilliseconds);
        if (remaining > TimeSpan.Zero)
        {
            signal.Wait(remaining);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();
        if (!_writerThread.Join(TimeSpan.FromSeconds(2)))
        {
            System.Diagnostics.Debug.WriteLine("FileLogger: writer thread did not flush within timeout.");
        }

        _queue.Dispose();
        GC.SuppressFinalize(this);
    }

    private bool Enqueue(LogItem item)
    {
        try
        {
            // Non-blocking: drop the entry when the queue is full so logging never stalls a caller.
            return _queue.TryAdd(item);
        }
        catch (ObjectDisposedException)
        {
            // Provider was disposed concurrently.
            return false;
        }
        catch (InvalidOperationException)
        {
            // Adding was completed on another thread during shutdown.
            return false;
        }
    }

    private bool TryAddWithTimeout(LogItem item, TimeSpan timeout)
    {
        try
        {
            return _queue.TryAdd(item, timeout);
        }
        catch (ObjectDisposedException)
        {
            // Provider was disposed concurrently.
            return false;
        }
        catch (InvalidOperationException)
        {
            // Adding was completed on another thread during shutdown.
            return false;
        }
    }

    private void ProcessQueue()
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FileLogger: failed to create log directory: {ex}");

            // Keep draining so enqueuers never block on a full queue and flush waiters are released.
            foreach (LogItem item in _queue.GetConsumingEnumerable())
            {
                Release(item.FlushSignal);
            }

            return;
        }

        foreach (LogItem item in _queue.GetConsumingEnumerable())
        {
            if (item.FlushSignal is not null)
            {
                Release(item.FlushSignal);
                continue;
            }

            try
            {
                RollIfNeeded();
                File.AppendAllText(_logFilePath, item.Line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FileLogger: failed to write log entry: {ex}");
            }
        }
    }

    private static void Release(ManualResetEventSlim? signal)
    {
        try
        {
            signal?.Set();
        }
        catch (ObjectDisposedException)
        {
            // The waiter timed out and disposed the signal; nothing to release.
        }
    }

    private void RollIfNeeded()
    {
        FileInfo fileInfo = new(_logFilePath);
        if (!fileInfo.Exists || fileInfo.Length < MaxLogFileSizeBytes)
        {
            return;
        }

        string oldest = FormattableString.Invariant($"{_logFilePath}.{MaxRetainedFiles}");
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (int i = MaxRetainedFiles - 1; i >= 1; i--)
        {
            string source = FormattableString.Invariant($"{_logFilePath}.{i}");
            if (File.Exists(source))
            {
                File.Move(source, FormattableString.Invariant($"{_logFilePath}.{i + 1}"));
            }
        }

        File.Move(_logFilePath, FormattableString.Invariant($"{_logFilePath}.1"));
    }

    /// <summary>A queued log line, or a flush marker that signals when prior entries are written.</summary>
    private sealed class LogItem
    {
        public LogItem(string? line, ManualResetEventSlim? flushSignal)
        {
            Line = line;
            FlushSignal = flushSignal;
        }

        public string? Line { get; }

        public ManualResetEventSlim? FlushSignal { get; }
    }
}

/// <summary>
/// Formats log entries and hands them to the owning <see cref="FileLoggerProvider"/>'s writer.
/// </summary>
internal sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly Action<string> _enqueue;

    public FileLogger(string categoryName, Action<string> enqueue)
    {
        _categoryName = categoryName;
        _enqueue = enqueue;
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);

        StringBuilder builder = new();
        builder.Append(CultureInfo.InvariantCulture, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{GetLevelLabel(logLevel)}] {_categoryName}");
        if (eventId.Id != 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $" ({eventId.Id})");
        }

        builder.Append(": ").Append(message);

        if (exception is not null)
        {
            builder.Append(Environment.NewLine).Append(exception);
        }

        _enqueue(builder.ToString());
    }

    private static string GetLevelLabel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "fail",
        LogLevel.Critical => "crit",
        _ => "none",
    };
}

/// <summary>
/// A no-op scope returned by <see cref="FileLogger.BeginScope{TState}(TState)"/>.
/// </summary>
internal sealed class NullScope : IDisposable
{
    public static NullScope Instance { get; } = new();

    private NullScope()
    {
    }

    public void Dispose()
    {
    }
}

internal static class FileLoggerBuilderExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register the concrete provider so crash/suspend handlers can resolve it to flush,
        // and expose the same instance to the logging factory as an ILoggerProvider.
        builder.Services.TryAddSingleton<FileLoggerProvider>();
        builder.Services.AddSingleton<ILoggerProvider>(static sp => sp.GetRequiredService<FileLoggerProvider>());
        return builder;
    }
}
