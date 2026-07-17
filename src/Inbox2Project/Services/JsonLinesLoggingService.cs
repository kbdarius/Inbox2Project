using System.Text.Json;

namespace Inbox2Project.Services;

public sealed class JsonLinesLoggingService : ILoggingService
{
    private readonly string _logRoot;

    public JsonLinesLoggingService(string? appDataPath = null)
    {
        var appData = appDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _logRoot = Path.Combine(appData, "Inbox2Project", "logs");
    }

    public Task LogInfoAsync(string eventName, object data, CancellationToken cancellationToken = default)
        => WriteAsync("INFO", eventName, data, cancellationToken);

    public Task LogErrorAsync(string eventName, object data, CancellationToken cancellationToken = default)
        => WriteAsync("ERROR", eventName, data, cancellationToken);

    private async Task WriteAsync(string level, string eventName, object data, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_logRoot);
        var logPath = Path.Combine(_logRoot, $"operations-{DateTime.UtcNow:yyyyMMdd}.log");
        var envelope = new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level,
            eventName,
            data,
        };
        var line = JsonSerializer.Serialize(envelope);
        await File.AppendAllTextAsync(logPath, line + Environment.NewLine, cancellationToken);
    }
}