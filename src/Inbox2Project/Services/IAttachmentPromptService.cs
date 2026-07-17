using Inbox2Project.Models;

namespace Inbox2Project.Services;

public interface IAttachmentPromptService
{
    Task<bool> ShouldIncludeAttachmentsAsync(OutlookItemSelection item, CancellationToken cancellationToken = default);
}
