using Inbox2Project.Core;
using Inbox2Project.Models;
using Inbox2Project.Outlook;
using Inbox2Project.OutlookBridge;
using Inbox2Project.Services;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBox = System.Windows.Forms.MessageBox;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

internal static class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        using var singleInstance = new Mutex(true, @"Local\Inbox2Project.OutlookBridge", out var isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }

        var mode = GetArgValue(args, "--include-attachments") ?? "ask";

        try
        {
            var selected = LoadSingleSelectionFromOutlook();

            var settingsService = new SettingsService();
            var loggingService = new JsonLinesLoggingService();
            var handler = new SaveToInbox2ProjectCommandHandler(
                new SelectionValidationService(),
                new ExportWorkflowService(
                    settingsService,
                    new ProjectDiscoveryService(),
                    new BridgeProjectSelectorUi(settingsService),
                    new BridgeAttachmentPromptService(mode),
                    new PathSafetyService(),
                    loggingService),
                loggingService);

            var command = new OutlookContextCommand(handler);
            var result = await command.ExecuteAsync(new[] { selected });

            if (result.Cancelled)
            {
                Environment.ExitCode = 0;
                return;
            }

            if (result.Succeeded)
            {
                using var completion = new CompletionForm(result.UserMessage, result.OutputPaths);
                completion.ShowDialog();
                Environment.ExitCode = 0;
                return;
            }

            MessageBox.Show(
                result.UserMessage + Environment.NewLine + Environment.NewLine + result.TechnicalMessage,
                AppInfo.WindowTitle("Error"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1);
            Environment.ExitCode = 1;
        }
        catch (System.Exception exception)
        {
            MessageBox.Show(
                "Could not execute Inbox2Project from Outlook selection.\n\n" + exception,
                AppInfo.WindowTitle("Outlook Bridge Error"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1);
            Environment.ExitCode = 1;
        }
    }

    private static OutlookItemSelection LoadSingleSelectionFromOutlook()
    {
        var appType = Type.GetTypeFromProgID("Outlook.Application")
            ?? throw new InvalidOperationException("Outlook COM automation is not available on this machine.");
        dynamic app = Activator.CreateInstance(appType)
            ?? throw new InvalidOperationException("Could not start Outlook COM automation.");
        object? explorer = null;
        object? selection = null;
        object? item = null;

        try
        {
            explorer = app.ActiveExplorer();
            if (explorer is null)
            {
                throw new InvalidOperationException("No active Outlook explorer was found.");
            }

            dynamic explorerDyn = explorer;
            selection = explorerDyn.Selection;
            dynamic selectionDyn = selection ?? throw new InvalidOperationException("Could not access Outlook selection.");
            if ((int)selectionDyn.Count != 1)
            {
                throw new InvalidOperationException("Select exactly one email in Outlook and retry.");
            }

            item = selectionDyn[1];
            dynamic mail = item;
            const int mailItemClass = 43;
            if ((int)mail.Class != mailItemClass)
            {
                throw new InvalidOperationException("Selected Outlook item is not a MailItem.");
            }

            var attachments = new List<AttachmentData>();
            int attachmentCount = (int)mail.Attachments.Count;
            for (var i = 1; i <= attachmentCount; i++)
            {
                dynamic attachment = mail.Attachments[i];
                try
                {
                    string fileName = attachment.FileName;
                    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "-" + fileName);
                    attachment.SaveAsFile(tempPath);
                    var content = File.ReadAllBytes(tempPath);
                    File.Delete(tempPath);
                    attachments.Add(new AttachmentData(fileName, content, IsInlineAttachment(attachment, fileName)));
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(attachment);
                }
            }

            DateTime received = mail.ReceivedTime;
            var receivedAt = received == DateTime.MinValue ? DateTimeOffset.Now : new DateTimeOffset(received);

            return new OutlookItemSelection(
                OutlookItemType.MailItem,
                ((string?)mail.Subject) ?? string.Empty,
                ((string?)mail.SenderName) ?? string.Empty,
                receivedAt,
                ((string?)mail.ConversationTopic) ?? string.Empty,
                ((string?)mail.Body) ?? string.Empty,
                attachments);
        }
        finally
        {
            if (item is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(item);
            if (selection is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(selection);
            if (explorer is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(explorer);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(app);
        }
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }

            return string.Empty;
        }

        return null;
    }

    private static bool IsInlineAttachment(dynamic attachment, string fileName)
    {
        const string contentIdProperty = "http://schemas.microsoft.com/mapi/proptag/0x3712001F";
        const string hiddenProperty = "http://schemas.microsoft.com/mapi/proptag/0x7FFE000B";
        string contentId = string.Empty;
        bool hidden = false;
        int position = -1;
        try { contentId = attachment.PropertyAccessor.GetProperty(contentIdProperty) as string ?? string.Empty; } catch { }
        try { hidden = attachment.PropertyAccessor.GetProperty(hiddenProperty) is true; } catch { }
        try { position = attachment.Position; } catch { }
        var extension = Path.GetExtension(fileName);
        var image = extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase);
        return image && (hidden || !string.IsNullOrWhiteSpace(contentId) || position >= 0);
    }

    private sealed class BridgeAttachmentPromptService : IAttachmentPromptService
    {
        private readonly string _mode;

        public BridgeAttachmentPromptService(string mode)
        {
            _mode = mode;
        }

        public Task<AttachmentSaveChoice> SelectAttachmentsAsync(OutlookItemSelection item, CancellationToken cancellationToken = default)
        {
            if (string.Equals(_mode, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new AttachmentSaveChoice(false, true, item.Attachments));
            }

            if (string.Equals(_mode, "no", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new AttachmentSaveChoice(false, true, Array.Empty<AttachmentData>()));
            }

            using var form = new AttachmentSelectionForm(item.Attachments);
            var result = form.ShowDialog();
            return Task.FromResult(result == DialogResult.OK ? form.Choice : new AttachmentSaveChoice(true, false, Array.Empty<AttachmentData>()));
        }
    }
}
