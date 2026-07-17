namespace Inbox2Project.Models;

public enum AppErrorId
{
    CfgRootMissing,
    CfgRootInvalid,
    PrjNoneFound,
    SelUnsupported,
    SelEmpty,
    TxtExportFailed,
    PromptFailed,
    FsWriteDenied,
    FsPathInvalid,
    AttSaveFailed,
    OutlookBusy,
    Unknown,
}

public sealed class AppException : Exception
{
    public AppException(AppErrorId errorId, string userMessage, string technicalMessage, Exception? innerException = null)
        : base(technicalMessage, innerException)
    {
        ErrorId = errorId;
        UserMessage = userMessage;
    }

    public AppErrorId ErrorId { get; }

    public string UserMessage { get; }
}

public static class ErrorCatalog
{
    public static (string UserMessage, string Guidance) Lookup(AppErrorId errorId) => errorId switch
    {
        AppErrorId.CfgRootMissing => ("Projects root is not configured.", "Open settings and select your Projects Root folder."),
        AppErrorId.CfgRootInvalid => ("Projects root path is invalid or unavailable.", "Verify the folder exists and you have access permissions."),
        AppErrorId.PrjNoneFound => ("No valid projects were found.", "Ensure project folders contain an EMAILS subfolder, then refresh."),
        AppErrorId.SelUnsupported => ("Selected item is not a supported email.", "Select a single email item and retry."),
        AppErrorId.SelEmpty => ("No email is selected.", "Select one email and run the command again."),
        AppErrorId.TxtExportFailed => ("Email text could not be saved.", "Check destination access and retry."),
        AppErrorId.PromptFailed => ("Could not confirm attachment export choice.", "Retry command. If issue persists, restart Outlook."),
        AppErrorId.FsWriteDenied => ("Cannot write to target folder.", "Check folder permissions or choose another project."),
        AppErrorId.FsPathInvalid => ("Target path is invalid.", "Shorten folder/file names or adjust project location."),
        AppErrorId.AttSaveFailed => ("Some attachments could not be saved.", "Review log details and retry export."),
        AppErrorId.OutlookBusy => ("Outlook is busy. Please retry.", "Wait a moment and try again."),
        _ => ("Unexpected error occurred.", "Retry and check logs for details."),
    };
}