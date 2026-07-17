using Inbox2Project.Core;
using Inbox2Project.Models;
using Inbox2Project.Outlook;
using Inbox2Project.Services;
using MessageBox = System.Windows.Forms.MessageBox;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;
using DialogResult = System.Windows.Forms.DialogResult;

var mode = GetArgValue(args, "--include-attachments") ?? "ask";

try
{
	var selected = LoadSingleSelectionFromOutlook();

	var loggingService = new JsonLinesLoggingService();
	var handler = new SaveToInbox2ProjectCommandHandler(
		new SelectionValidationService(),
		new ExportWorkflowService(
			new SettingsService(),
			new ProjectDiscoveryService(),
			new DefaultProjectSelectorUi(),
			new BridgeAttachmentPromptService(mode),
			new PathSafetyService(),
			loggingService),
		loggingService);

	var command = new OutlookContextCommand(handler);
	var result = await command.ExecuteAsync(new[] { selected });

	if (result.Succeeded)
	{
		MessageBox.Show(
			"Inbox2Project completed successfully.\n\n" + string.Join(Environment.NewLine, result.OutputPaths),
			"Inbox2Project",
			MessageBoxButtons.OK,
			MessageBoxIcon.Information,
			MessageBoxDefaultButton.Button1);
		Environment.ExitCode = 0;
		return;
	}

	MessageBox.Show(
		result.UserMessage + Environment.NewLine + Environment.NewLine + result.TechnicalMessage,
		"Inbox2Project - Error",
		MessageBoxButtons.OK,
		MessageBoxIcon.Error,
		MessageBoxDefaultButton.Button1);
	Environment.ExitCode = 1;
}
catch (System.Exception exception)
{
	MessageBox.Show(
		"Could not execute Inbox2Project from Outlook selection.\n\n" + exception,
		"Inbox2Project - Outlook Bridge",
		MessageBoxButtons.OK,
		MessageBoxIcon.Error,
		MessageBoxDefaultButton.Button1);
	Environment.ExitCode = 1;
}

static OutlookItemSelection LoadSingleSelectionFromOutlook()
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
			string fileName = attachment.FileName;
			var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + "-" + fileName);
			attachment.SaveAsFile(tempPath);
			var content = File.ReadAllBytes(tempPath);
			File.Delete(tempPath);
			attachments.Add(new AttachmentData(fileName, content));
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

static string? GetArgValue(string[] args, string name)
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

sealed class BridgeAttachmentPromptService : IAttachmentPromptService
{
	private readonly string _mode;

	public BridgeAttachmentPromptService(string mode)
	{
		_mode = mode;
	}

	public Task<bool> ShouldIncludeAttachmentsAsync(OutlookItemSelection item, CancellationToken cancellationToken = default)
	{
		if (string.Equals(_mode, "yes", StringComparison.OrdinalIgnoreCase))
		{
			return Task.FromResult(true);
		}

		if (string.Equals(_mode, "no", StringComparison.OrdinalIgnoreCase))
		{
			return Task.FromResult(false);
		}

		var result = MessageBox.Show(
			"Attachments were detected. Include attachments in export?",
			"Inbox2Project",
			MessageBoxButtons.YesNo,
			MessageBoxIcon.Question,
			MessageBoxDefaultButton.Button2);

		return Task.FromResult(result == DialogResult.Yes);
	}
}
