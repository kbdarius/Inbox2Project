using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed record OllamaSetupState(bool IsServerAvailable, bool IsModelAvailable);

public interface IAiFolderNameService
{
    string ModelName { get; }

    string DownloadUrl { get; }

    Task<OllamaSetupState> GetSetupStateAsync(CancellationToken cancellationToken = default);

    Task<string?> SuggestFolderNameAsync(string subject, string bodyText, CancellationToken cancellationToken = default);
}
