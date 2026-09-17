using Inbox2Project.Models;
using Inbox2Project.Services;
using Xunit;

namespace Inbox2Project.Tests;

public sealed class ExportWorkflowSubprojectTests
{
    [Fact]
    public async Task ExportAsync_IncludesSelectedSubprojectInEmailAndAttachmentFolderNames()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Inbox2Project.Tests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(tempRoot, "AppData");
        var destination = Path.Combine(tempRoot, "Li610", "EMAILS");
        Directory.CreateDirectory(destination);

        try
        {
            var settingsService = new SettingsService(appDataPath);
            await settingsService.AddProjectAsync("Li610", destination);
            var selector = new FixedProjectSelector(new ProjectSelectionResult(destination, "Design Review", false, "OPUS"));
            var workflow = new ExportWorkflowService(
                settingsService,
                new ProjectDiscoveryService(),
                selector,
                new DefaultAttachmentPromptService(defaultDecision: true),
                new PathSafetyService(),
                new NoOpAiFolderNameService(),
                new JsonLinesLoggingService(appDataPath));
            var item = new OutlookItemSelection(
                OutlookItemType.MailItem,
                "Design Review",
                "",
                new DateTimeOffset(2026, 9, 17, 8, 30, 0, TimeSpan.FromHours(-4)),
                "Design Review",
                "Email body",
                [new AttachmentData("plan.pdf", [1, 2, 3])]);

            var result = await workflow.ExportAsync(item);

            Assert.True(result.Succeeded, result.TechnicalMessage);
            var outputFolder = Assert.Single(Directory.GetDirectories(destination));
            Assert.Equal("20260917_OPUS_Design Review", Path.GetFileName(outputFolder));
            Assert.Contains(result.OutputPaths, path =>
                Path.GetFileName(path) == "20260917_OPUS_Design Review.txt");
            Assert.Contains(result.OutputPaths, path => Path.GetFileName(path) == "plan.pdf");
            Assert.All(result.OutputPaths, path => Assert.Equal(outputFolder, Path.GetDirectoryName(path)));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class FixedProjectSelector(ProjectSelectionResult result) : IProjectSelectorUi
    {
        public Task<ProjectSelectionResult?> SelectProjectAsync(
            IReadOnlyList<string> projectPaths,
            string? suggestedProjectPath,
            string suggestedBaseName,
            string senderName,
            DateTimeOffset receivedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProjectSelectionResult?>(result);
        }
    }
}
