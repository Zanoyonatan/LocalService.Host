using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace LocalService.Host.Infra;

public sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;
    private readonly string _logDirectory;
    private readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>());
    private readonly CancellationTokenSource _cts = new();
    private int _started;

    public SimpleFileLoggerProvider(string path)
    {
        _logFilePath = path;

        _logDirectory = Path.GetDirectoryName(_logFilePath);
        //if (!string.IsNullOrWhiteSpace(_path))
        //    Directory.CreateDirectory(_path);

       // Task.Run(WriterLoop);
    }

    //lazy start the writer loop when first logger is created, to avoid startup crach in IO issues (e.g. network drive not ready)
    private void EnsureStarted()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

        Task.Run(WriterLoop);
    }

    public ILogger CreateLogger(string categoryName)
        => new SimpleFileLogger(this,_queue, categoryName);

    public void Dispose()
    {
        _cts.Cancel();
        _queue.CompleteAdding();
    }

    private async Task WriterLoop()
    {
        try
        {

            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
            while (!_cts.IsCancellationRequested && !_queue.IsCompleted)
            {
                if (!_queue.TryTake(out var line, TimeSpan.FromMilliseconds(250)))
                    continue;

                await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {

            // service must not crash (network 
           string logCrashFilePath = $"{Path.Combine( Path.GetTempPath() ,"_AvivCrash.log")}";
            try
            {
                 await File.AppendAllTextAsync(logCrashFilePath, ex.Message);
            }
            catch 
            {
            }
         

        }
    }

    private sealed class SimpleFileLogger : ILogger
    {
        private readonly SimpleFileLoggerProvider _provider;
        private readonly BlockingCollection<string> _queue;
        private readonly string _category;

        public SimpleFileLogger(SimpleFileLoggerProvider provider, BlockingCollection<string> queue, string category)
        {
            _provider = provider;   
            _queue = queue;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _provider.EnsureStarted();

            var msg = formatter(state, exception);
            var line =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_category} - {msg}";

            if (exception != null)
                line += $" | EX: {exception.GetType().Name}: {exception.Message}";

            if (!_queue.IsAddingCompleted)
                _queue.Add(line);
        }
    }
}
