namespace Inbox2Project.Services;

public sealed class DefaultDuplicateSubjectPromptService : IDuplicateSubjectPromptService
{
    public Task<DuplicateSubjectDecision> ResolveAsync(string existingItemName, string subject, CancellationToken cancellationToken = default)
    {
        // Non-interactive contexts (DevHarness, headless callers) should never silently
        // overwrite existing files, so always fall back to creating a new folder.
        return Task.FromResult(DuplicateSubjectDecision.CreateNewFolder);
    }
}
