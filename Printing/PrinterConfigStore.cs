using System.Text.Json;

namespace LocalService.Host.Printing;

public sealed class PrinterConfigStore
{
    private readonly string _configPath;
    private readonly object _lock = new();
    private PrinterConfig? _cached;
    private DateTime _lastReadUtc;

    public PrinterConfigStore(string configPath)
    {
        _configPath = configPath;
    }

    public PrinterMapping? TryGetMapping(string documentType)
    {
        var cfg = GetConfig();
        if (cfg.DocumentMappings.TryGetValue(documentType, out var mapping))
            return mapping;

        return null;
    }

    private PrinterConfig GetConfig()
    {
        lock (_lock)
        {
            // simple cache: reread if file changed in last 3 seconds or no cache
            // (keeps things fast and stable; we can upgrade to FileSystemWatcher later)
            if (_cached is null || DateTime.UtcNow - _lastReadUtc > TimeSpan.FromSeconds(3))
            {
                var json = File.ReadAllText(_configPath);
                _cached = JsonSerializer.Deserialize<PrinterConfig>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new PrinterConfig();

                _lastReadUtc = DateTime.UtcNow;
            }

            return _cached;
        }
    }
}

public sealed class PrinterConfig
{
    public Dictionary<string, PrinterMapping> DocumentMappings { get; set; } = new();
}

public sealed class PrinterMapping
{
    public string PrinterName { get; set; } = "";
    public string Tray { get; set; } = "Auto"; // Upper/Lower/Middle/Auto etc.
}
