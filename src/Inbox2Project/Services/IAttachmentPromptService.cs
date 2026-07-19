using Inbox2Project.Models;

namespace Inbox2Project.Services;

public interface IAttachmentPromptService
{
    Task<AttachmentSaveChoice> SelectAttachmentsAsync(OutlookItemSelection item, CancellationToken cancellationToken = default);
}
