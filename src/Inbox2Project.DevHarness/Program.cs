using Inbox2Project.Core;
using Inbox2Project.Models;
using Inbox2Project.Outlook;
using Inbox2Project.Services;

var mode = args.Length > 0 ? args[0] : "no-attachments";
// IMPORTANT: DevHarness must NEVER share the real Inbox2Project app-data folder
// (%APPDATA%\Inbox2Project) - that file also backs the real Outlook add-in and
// this harness overwrites settings.json with sample data on every run, which
// previously wiped out the user's real saved project locations.
// SettingsService/JsonLinesLoggingService both append an "Inbox2Project" segment
// to whatever appDataPath they're given, so devHarnessAppDataRoot must be the
// FAKE "AppData" root (one level above the "Inbox2Project" folder those services
// will actually read/write).
var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var devHarnessAppDataRoot = Path.Combine(appData, "Inbox2Project.DevHarness");
var inboxRoot = Path.Combine(devHarnessAppDataRoot, "Inbox2Project");
var projectsRoot = Path.Combine(inboxRoot, "Projects");
var projectPath = Path.Combine(projectsRoot, "DevHarnessProject");
var emailsPath = Path.Combine(projectPath, "EMAILS");
var settingsPath = Path.Combine(inboxRoot, "settings.json");

Directory.CreateDirectory(emailsPath);
await File.WriteAllTextAsync(settingsPath, "{\"ProjectsRoot\":\"" + EscapeJson(projectsRoot) + "\",\"LastSelectedProject\":\"" + EscapeJson(projectPath) + "\",\"SavedProjects\":[{\"Name\":\"DevHarnessProject\",\"ProjectPath\":\"" + EscapeJson(projectPath) + "\"}]}");

var includeAttachments = string.Equals(mode, "attachments-yes", StringComparison.OrdinalIgnoreCase);
var withAttachments = !string.Equals(mode, "no-attachments", StringComparison.OrdinalIgnoreCase);

var loggingService = new JsonLinesLoggingService(devHarnessAppDataRoot);
var handler = new SaveToInbox2ProjectCommandHandler(
	new SelectionValidationService(),
	new ExportWorkflowService(
		new SettingsService(devHarnessAppDataRoot),
		new ProjectDiscoveryService(),
		new DefaultProjectSelectorUi(),
		new DefaultAttachmentPromptService(includeAttachments),
		new PathSafetyService(),
		new NoOpAiFolderNameService(),
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
