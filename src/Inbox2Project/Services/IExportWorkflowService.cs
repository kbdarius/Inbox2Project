using Inbox2Project.Models;

namespace Inbox2Project.Services;

public interface IExportWorkflowService
{
    Task<OperationResult> ExportAsync(OutlookItemSelection item, CancellationToken cancellationToken = default);
}
