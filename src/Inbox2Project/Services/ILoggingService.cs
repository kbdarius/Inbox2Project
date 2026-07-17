namespace Inbox2Project.Services;

public interface ILoggingService
{
    Task LogInfoAsync(string eventName, object data, CancellationToken cancellationToken = default);

    Task LogErrorAsync(string eventName, object data, CancellationToken cancellationToken = default);
}