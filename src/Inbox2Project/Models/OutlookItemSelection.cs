namespace Inbox2Project.Models;

public enum OutlookItemType
{
    Unknown = 0,
    MailItem = 1,
    Other = 2,
}

public sealed record OutlookItemSelection(
    OutlookItemType ItemType,
    string Subject,
    string Sender,
    DateTimeOffset ReceivedAt,
    string ConversationTopic,
    string BodyText,
    IReadOnlyList<AttachmentData> Attachments,
    Action<string>? SaveAsOutlookMessage = null);

public sealed record AttachmentData(string FileName, byte[] Content, bool IsInline = false)
{
    public string DisplayText => $"{FileName} ({FormatSize(Content.LongLength)})" + (IsInline ? " - inline/signature image" : string.Empty);
    public override string ToString() => DisplayText;

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_048_576 => $"{bytes / 1_048_576d:0.0} MB",
        >= 1_024 => $"{bytes / 1_024d:0.0} KB",
        _ => $"{bytes} B",
    };
}

public sealed record AttachmentSaveChoice(
    bool Cancelled,
    bool IncludeEmail,
    IReadOnlyList<AttachmentData> Attachments);
