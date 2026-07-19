using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class DefaultAttachmentPromptService : IAttachmentPromptService
{
    private readonly bool _defaultDecision;

    public DefaultAttachmentPromptService(bool defaultDecision = false)
    {
        _defaultDecision = defaultDecision;
    }

    public Task<AttachmentSaveChoice> SelectAttachmentsAsync(OutlookItemSelection item, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AttachmentData> selected = _defaultDecision ? item.Attachments : Array.Empty<AttachmentData>();
        return Task.FromResult(new AttachmentSaveChoice(false, true, selected));
    }
}
