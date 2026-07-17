using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class DefaultAttachmentPromptService : IAttachmentPromptService
{
    private readonly bool _defaultDecision;

    public DefaultAttachmentPromptService(bool defaultDecision = false)
    {
        _defaultDecision = defaultDecision;
    }

    public Task<bool> ShouldIncludeAttachmentsAsync(OutlookItemSelection item, CancellationToken cancellationToken = default)
        => Task.FromResult(_defaultDecision);
}
