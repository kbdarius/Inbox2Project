using Inbox2Project.Models;
using Inbox2Project.Services;

namespace Inbox2Project.Core;

public sealed class SaveToInbox2ProjectCommandHandler
{
    private readonly ISelectionValidationService _selectionValidationService;
    private readonly IExportWorkflowService _exportWorkflowService;
    private readonly ILoggingService _loggingService;

    public SaveToInbox2ProjectCommandHandler(
        ISelectionValidationService selectionValidationService,
        IExportWorkflowService exportWorkflowService,
        ILoggingService loggingService)
    {
        _selectionValidationService = selectionValidationService;
        _exportWorkflowService = exportWorkflowService;
        _loggingService = loggingService;
    }

    public async Task<OperationResult> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken = default)
    {
        try
        {
            var selectedMail = _selectionValidationService.ValidateSingleMailItem(invocation.SelectedItems);
            await _loggingService.LogInfoAsync(
                "command_invoked",
                new
                {
                    invocation.CommandName,
                    selectionCount = invocation.SelectedItems.Count,
                    selectedItemType = selectedMail.ItemType.ToString(),
                    selectedMail.Subject,
                    selectedMail.ConversationTopic,
                    invocation.TriggeredAt,
                },
                cancellationToken);

            return await _exportWorkflowService.ExportAsync(selectedMail, cancellationToken);
        }
        catch (AppException appException)
        {
            await _loggingService.LogErrorAsync(
                "command_failed",
                new
                {
                    errorId = appException.ErrorId.ToString(),
                    appException.UserMessage,
                    technicalMessage = appException.Message,
                },
                cancellationToken);

            return OperationResult.Failure(appException.ErrorId, appException.UserMessage, appException.Message);
        }
        catch (Exception exception)
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.Unknown);
            await _loggingService.LogErrorAsync(
                "command_failed",
                new
                {
                    errorId = AppErrorId.Unknown.ToString(),
                    userMessage,
                    technicalMessage = exception.ToString(),
                },
                cancellationToken);

            return OperationResult.Failure(AppErrorId.Unknown, userMessage, exception.ToString());
        }
    }
}