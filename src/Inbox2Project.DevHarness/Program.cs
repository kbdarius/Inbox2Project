using Inbox2Project.Core;
using Inbox2Project.Models;
using Inbox2Project.Outlook;
using Inbox2Project.Services;

var mode = args.Length > 0 ? args[0] : "no-attachments";
var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var inboxRoot = Path.Combine(appData, "Inbox2Project");
var projectsRoot = Path.Combine(inboxRoot, "Projects");
var projectPath = Path.Combine(projectsRoot, "SampleProject");
var emailsPath = Path.Combine(projectPath, "EMAILS");
var settingsPath = Path.Combine(inboxRoot, "settings.json");

Directory.CreateDirectory(emailsPath);
await File.WriteAllTextAsync(settingsPath, "{\"ProjectsRoot\":\"" + EscapeJson(projectsRoot) + "\",\"LastSelectedProject\":\"" + EscapeJson(projectPath) + "\"}");

var includeAttachments = string.Equals(mode, "attachments-yes", StringComparison.OrdinalIgnoreCase);
var withAttachments = !string.Equals(mode, "no-attachments", StringComparison.OrdinalIgnoreCase);

var loggingService = new JsonLinesLoggingService();
var handler = new SaveToInbox2ProjectCommandHandler(
	new SelectionValidationService(),
	new ExportWorkflowService(
		new SettingsService(),
		new ProjectDiscoveryService(),
		new DefaultProjectSelectorUi(),
		new DefaultAttachmentPromptService(includeAttachments),
		new PathSafetyService(),
		loggingService),
	loggingService);

var command = new OutlookContextCommand(handler);
var selection = CreateSelection(withAttachments);
var result = await command.ExecuteAsync(new[] { selection });

Console.WriteLine($"Mode: {mode}");
Console.WriteLine($"Succeeded: {result.Succeeded}");
Console.WriteLine($"Message: {result.UserMessage}");
if (result.OutputPaths.Count > 0)
{
	Console.WriteLine("Outputs:");
	foreach (var output in result.OutputPaths)
	{
		Console.WriteLine("- " + output);
	}
}

return;

static OutlookItemSelection CreateSelection(bool withAttachments)
{
	var attachments = new List<AttachmentData>();
	if (withAttachments)
	{
		attachments.Add(new AttachmentData("quote.pdf", new byte[] { 1, 2, 3, 4, 5 }));
		attachments.Add(new AttachmentData("screenshot.png", new byte[] { 6, 7, 8, 9, 10 }));
	}

	return new OutlookItemSelection(
		OutlookItemType.MailItem,
		"RE: Intake request / sprint planning",
		"sender@example.com",
		DateTimeOffset.UtcNow,
		"Intake request",
		"This is a sample exported email body used for local validation.",
		attachments);
}

static string EscapeJson(string value)
{
	return value.Replace("\\", "\\\\");
}
