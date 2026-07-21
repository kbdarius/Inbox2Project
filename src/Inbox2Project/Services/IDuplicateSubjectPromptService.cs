namespace Inbox2Project.Services;

public enum DuplicateSubjectDecision
{
    Cancel,
    UseExistingFolder,
    CreateNewFolder,
}

public interface IDuplicateSubjectPromptService
{
    Task<DuplicateSubjectDecision> ResolveAsync(string existingItemName, string subject, CancellationToken cancellationToken = default);
}
