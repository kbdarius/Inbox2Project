using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed record AiFolderNameSetupState(
    bool IsConfigured,
    bool IsServiceAvailable,
    bool IsModelAvailable,
    string? SelectedModelName,
    IReadOnlyList<string> AvailableModelNames,
    string SetupUrl,
    string? StatusMessage = null);

public interface IAiFolderNameService
{
    string ModelName { get; }

    string SetupUrl { get; }

    Task<AiFolderNameSetupState> GetSetupStateAsync(CancellationToken cancellationToken = default);

    Task<string?> SuggestFolderNameAsync(string subject, string bodyText, CancellationToken cancellationToken = default);
}
