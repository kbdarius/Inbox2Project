using Inbox2Project.Core;
using Inbox2Project.Models;

namespace Inbox2Project.Outlook;

public sealed class OutlookContextCommand
{
    public const string SaveCommandName = "Save to Inbox2Project";

    private readonly SaveToInbox2ProjectCommandHandler _handler;

    public OutlookContextCommand(SaveToInbox2ProjectCommandHandler handler)
    {
        _handler = handler;
    }

    public Task<OperationResult> ExecuteAsync(
        IReadOnlyList<OutlookItemSelection> selectedItems,
        CancellationToken cancellationToken = default)
    {
        var invocation = new CommandInvocation(SaveCommandName, selectedItems, DateTimeOffset.UtcNow);
        return _handler.HandleAsync(invocation, cancellationToken);
    }
}