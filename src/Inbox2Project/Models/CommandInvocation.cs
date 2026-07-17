namespace Inbox2Project.Models;

public sealed record CommandInvocation(
    string CommandName,
    IReadOnlyList<OutlookItemSelection> SelectedItems,
    DateTimeOffset TriggeredAt);