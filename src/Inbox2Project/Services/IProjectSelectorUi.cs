namespace Inbox2Project.Services;

public interface IProjectSelectorUi
{
    Task<string> SelectProjectAsync(IReadOnlyList<string> projectPaths, string? suggestedProjectPath, CancellationToken cancellationToken = default);
}
