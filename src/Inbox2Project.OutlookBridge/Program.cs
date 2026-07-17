using Inbox2Project.Core;
using Inbox2Project.Models;
using Inbox2Project.Outlook;
using Inbox2Project.Services;
using Microsoft.Office.Interop.Outlook;
using Application = Microsoft.Office.Interop.Outlook.Application;
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
	var app = new Application();
	Explorer? explorer = null;
	Selection? selection = null;

	try
	{
		explorer = app.ActiveExplorer();
		if (explorer is null)
		{
			throw new InvalidOperationException("No active Outlook explorer was found.");
		}

		selection = explorer.Selection;
		if (selection is null || selection.Count != 1)
		{
			throw new InvalidOperationException("Select exactly one email in Outlook and retry.");
		}

		var item = selection[1];
		if (item is not MailItem mail)
		{
			throw new InvalidOperationException("Selected Outlook item is not a MailItem.");
		}

		var attachments = new List<AttachmentData>();
		for (var i = 1; i <= mail.Attachments.Count; i++)
		{
			var attachment = mail.Attachments[i];
			var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + "-" + attachment.FileName);
			attachment.SaveAsFile(tempPath);
			var content = File.ReadAllBytes(tempPath);
			File.Delete(tempPath);
			attachments.Add(new AttachmentData(attachment.FileName, content));
		}

		return new OutlookItemSelection(
			OutlookItemType.MailItem,
			mail.Subject ?? string.Empty,
			mail.SenderName ?? string.Empty,
			new DateTimeOffset(mail.ReceivedTime == DateTime.MinValue ? DateTime.Now : mail.ReceivedTime),
			mail.ConversationTopic ?? string.Empty,
			mail.Body ?? string.Empty,
			attachments);
	}
	finally
	{
		if (selection is not null) System.Runtime.InteropServices.Marshal.ReleaseComObject(selection);
		if (explorer is not null) System.Runtime.InteropServices.Marshal.ReleaseComObject(explorer);
		System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
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
