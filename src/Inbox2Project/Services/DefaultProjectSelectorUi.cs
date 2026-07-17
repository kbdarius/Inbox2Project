namespace Inbox2Project.Services;

public sealed class DefaultProjectSelectorUi : IProjectSelectorUi
{
    public Task<string> SelectProjectAsync(IReadOnlyList<string> projectPaths, string? suggestedProjectPath, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(suggestedProjectPath) && projectPaths.Contains(suggestedProjectPath, StringComparer.OrdinalIgnoreCase))
        {
            return Task.FromResult(suggestedProjectPath);
        }

        return Task.FromResult(projectPaths[0]);
    }
}
