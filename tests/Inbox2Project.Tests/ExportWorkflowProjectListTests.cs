using System.Text.Json;
using Inbox2Project.Models;
using Inbox2Project.Services;
using Xunit;

namespace Inbox2Project.Tests;

public sealed class ExportWorkflowProjectListTests
{
    [Fact]
    public async Task ExportAsync_PassesSavedProjectsEvenWhenTheirFoldersAreUnavailable()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Inbox2Project.Tests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(tempRoot, "AppData");
        var projectsRoot = Path.Combine(tempRoot, "Projects");
        var availablePath = Path.Combine(projectsRoot, "AvailableProject");
        var unavailablePath = Path.Combine(projectsRoot, "UnavailableProject");
        Directory.CreateDirectory(appDataPath);
        Directory.CreateDirectory(availablePath);
        Directory.CreateDirectory(Path.Combine(appDataPath, "Inbox2Project"));

        try
        {
            var settings = new SettingsModel
            {
                ProjectsRoot = projectsRoot,
                SavedProjects =
                [
                    new SavedProjectDefinition { Name = "Available", ProjectPath = availablePath },
                    new SavedProjectDefinition { Name = "Unavailable", ProjectPath = unavailablePath },
                ],
            };
            await File.WriteAllTextAsync(
                Path.Combine(appDataPath, "Inbox2Project", "settings.json"),
                JsonSerializer.Serialize(settings));

            var selector = new CapturingProjectSelector();
            var workflow = new ExportWorkflowService(
                new SettingsService(appDataPath),
                new ProjectDiscoveryService(),
                selector,
                new DefaultAttachmentPromptService(),
                new PathSafetyService(),
                new NoOpAiFolderNameService(),
                new JsonLinesLoggingService(appDataPath));

            var result = await workflow.ExportAsync(new OutlookItemSelection(
                OutlookItemType.MailItem,
                "Subject",
                "Sender",
                DateTimeOffset.UtcNow,
                "Subject",
                "Body",
                Array.Empty<AttachmentData>()));

            Assert.True(result.Cancelled);
            Assert.Equal(new[] { availablePath, unavailablePath }, selector.ProjectPaths);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class CapturingProjectSelector : IProjectSelectorUi
    {
        public IReadOnlyList<string> ProjectPaths { get; private set; } = Array.Empty<string>();

        public Task<ProjectSelectionResult?> SelectProjectAsync(
            IReadOnlyList<string> projectPaths,
            string? suggestedProjectPath,
            string suggestedBaseName,
            string senderName,
            DateTimeOffset receivedAt,
            CancellationToken cancellationToken = default)
        {
            ProjectPaths = projectPaths.ToArray();
            return Task.FromResult<ProjectSelectionResult?>(null);
        }
    }
}
