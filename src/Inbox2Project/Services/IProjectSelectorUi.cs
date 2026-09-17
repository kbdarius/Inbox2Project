namespace Inbox2Project.Services;

public sealed record ProjectSelectionResult(
    string ProjectPath,
    string FinalBaseName,
    bool SaveEmailAsMsg = false,
    string? SubprojectName = null);

public interface IProjectSelectorUi
{
    Task<ProjectSelectionResult?> SelectProjectAsync(
        IReadOnlyList<string> projectPaths,
        string? suggestedProjectPath,
        string suggestedBaseName,
        string senderName,
    DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default);
}
