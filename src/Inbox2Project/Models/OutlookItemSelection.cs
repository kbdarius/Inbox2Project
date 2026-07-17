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
    IReadOnlyList<AttachmentData> Attachments);

public sealed record AttachmentData(string FileName, byte[] Content);