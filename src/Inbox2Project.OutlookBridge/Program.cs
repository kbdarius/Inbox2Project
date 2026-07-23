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
            using var mailExporter = LoadSingleSelectionFromOutlook(out var selected);
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var aiFolderNameService = new OpenAiFolderNameService(httpClient);

            var settingsService = new SettingsService();
            var loggingService = new JsonLinesLoggingService();
            var handler = new SaveToInbox2ProjectCommandHandler(
                new SelectionValidationService(),
                new ExportWorkflowService(
                    settingsService,
                    new ProjectDiscoveryService(),
                    new BridgeProjectSelectorUi(settingsService, aiFolderNameService),
                    new BridgeAttachmentPromptService(mode),
                    new PathSafetyService(),
                    aiFolderNameService,
                    loggingService,
                    new BridgeDuplicateSubjectPromptService()),
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

    private static OutlookMailExporter LoadSingleSelectionFromOutlook(out OutlookItemSelection selection)
    {
        var appType = Type.GetTypeFromProgID("Outlook.Application")
            ?? throw new InvalidOperationException("Outlook COM automation is not available on this machine.");
        dynamic app = Activator.CreateInstance(appType)
            ?? throw new InvalidOperationException("Could not start Outlook COM automation.");
        object? explorer = null;
        object? itemSelection = null;
        object? item = null;

        try
        {
            explorer = app.ActiveExplorer();
            if (explorer is null)
            {
                throw new InvalidOperationException("No active Outlook explorer was found.");
            }

            dynamic explorerDyn = explorer;
            itemSelection = explorerDyn.Selection;
            dynamic selectionDyn = itemSelection ?? throw new InvalidOperationException("Could not access Outlook selection.");
            if ((int)selectionDyn.Count != 1)
            {
                throw new InvalidOperationException("Select exactly one email in Outlook and retry.");
            }

            item = selectionDyn[1];
            const int mailItemClass = 43;
            var itemClass = TryGetInt32Property(item, "Class");
            var messageClass = TryGetStringProperty(item, "MessageClass") ?? string.Empty;
            var isMailLike = itemClass == mailItemClass || messageClass.StartsWith("IPM.Note", StringComparison.OrdinalIgnoreCase);
            if (!isMailLike)
            {
                throw new InvalidOperationException($"Selected Outlook item is not a supported mail item. Class={itemClass?.ToString() ?? "unknown"}, MessageClass={messageClass}.");
            }

            dynamic mail = item;

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

            var exporter = new OutlookMailExporter((object)mail);
            item = null;

            // The overall constructor call below is dynamically bound (several arguments are
            // derived from the dynamic `mail` object), and a bare method group can't be passed
            // as an argument to a dynamically dispatched call (CS1976) - so materialize the
            // delegate into a plain, statically-typed local first.
            Action<string> saveAsMsg = exporter.SaveAsMsg;

            selection = new OutlookItemSelection(
                OutlookItemType.MailItem,
                TryGetStringProperty(mail, "Subject") ?? string.Empty,
                TryGetStringProperty(mail, "SenderName") ?? string.Empty,
                receivedAt,
                TryGetStringProperty(mail, "ConversationTopic") ?? string.Empty,
                TryGetStringProperty(mail, "Body") ?? string.Empty,
                attachments,
                saveAsMsg);
            return exporter;
        }
        finally
        {
            if (item is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(item);
            if (itemSelection is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(itemSelection);
            if (explorer is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(explorer);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(app);
        }
    }

    private sealed class OutlookMailExporter : IDisposable
    {
        private readonly object _mail;
        private bool _disposed;

        public OutlookMailExporter(object mail)
        {
            _mail = mail;
        }

        public void SaveAsMsg(string path)
        {
            const int olMsgUnicode = 9;
            dynamic mail = _mail;
            mail.SaveAs(path, olMsgUnicode);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(_mail);
        }
    }

    private static int? TryGetInt32Property(object source, string propertyName)
    {
        try
        {
            var value = source.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.GetProperty,
                null,
                source,
                Array.Empty<object>());
            return Convert.ToInt32(value);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetStringProperty(object source, string propertyName)
    {
        try
        {
            var value = source.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.GetProperty,
                null,
                source,
                Array.Empty<object>());
            return value as string ?? Convert.ToString(value);
        }
        catch
        {
            return null;
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

    private sealed class BridgeDuplicateSubjectPromptService : IDuplicateSubjectPromptService
    {
        public Task<DuplicateSubjectDecision> ResolveAsync(string existingItemName, string subject, CancellationToken cancellationToken = default)
        {
            using var form = new DuplicateSubjectPromptForm(existingItemName, subject);
            var result = form.ShowDialog();
            var decision = result == DialogResult.OK ? form.Decision : DuplicateSubjectDecision.Cancel;
            return Task.FromResult(decision);
        }
    }
}
