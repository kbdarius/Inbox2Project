namespace Inbox2Project.Services;

public sealed class DefaultProjectSelectorUi : IProjectSelectorUi
{
    public Task<ProjectSelectionResult?> SelectProjectAsync(
        IReadOnlyList<string> projectPaths,
        string? suggestedProjectPath,
        string suggestedBaseName,
        string senderName,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        var projectPath = !string.IsNullOrWhiteSpace(suggestedProjectPath) && projectPaths.Contains(suggestedProjectPath, StringComparer.OrdinalIgnoreCase)
            ? suggestedProjectPath
            : projectPaths[0];

        return Task.FromResult<ProjectSelectionResult?>(new ProjectSelectionResult(projectPath, suggestedBaseName));
    }
}
