using Inbox2Project.Models;
using Inbox2Project.Services;
using Xunit;

namespace Inbox2Project.Tests;

public sealed class ExportWorkflowAiProviderTests
{
    [Fact]
    public async Task ExportAsync_UsesCopilotFolderNameServiceWhenCopilotIsSelected()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Inbox2Project.Tests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(tempRoot, "AppData");

        try
        {
            var settingsService = new SettingsService(appDataPath);
            await settingsService.SaveAiProviderAsync(AiNamingProvider.Copilot);
            var copilotService = new FixedAiFolderNameService("Copilot Suggested Name");
            var selector = new CapturingProjectSelector();
            var workflow = new ExportWorkflowService(
                settingsService,
                new ProjectDiscoveryService(),
                selector,
                new DefaultAttachmentPromptService(),
                new PathSafetyService(),
                new NoOpAiFolderNameService(),
                new JsonLinesLoggingService(appDataPath),
                copilotFolderNameService: copilotService);

            var result = await workflow.ExportAsync(new OutlookItemSelection(
                OutlookItemType.MailItem,
                "Original Subject",
                "Sender",
                DateTimeOffset.UtcNow,
                "Original Subject",
                "Email body",
                Array.Empty<AttachmentData>()));

            Assert.True(result.Cancelled);
            Assert.Equal("Copilot Suggested Name", selector.SuggestedBaseName);
            Assert.Equal(1, copilotService.SuggestionCount);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class FixedAiFolderNameService(string suggestion) : IAiFolderNameService
    {
        public int SuggestionCount { get; private set; }

        public string ModelName => "test-model";

        public string SetupUrl => "https://example.com";

        public Task<AiFolderNameSetupState> GetSetupStateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiFolderNameSetupState(
                true,
                true,
                true,
                ModelName,
                Array.Empty<string>(),
                SetupUrl));
        }

        public Task<string?> SuggestFolderNameAsync(
            string subject,
            string bodyText,
            CancellationToken cancellationToken = default)
        {
            SuggestionCount++;
            return Task.FromResult<string?>(suggestion);
        }
    }

    private sealed class CapturingProjectSelector : IProjectSelectorUi
    {
        public string? SuggestedBaseName { get; private set; }

        public Task<ProjectSelectionResult?> SelectProjectAsync(
            IReadOnlyList<string> projectPaths,
            string? suggestedProjectPath,
            string suggestedBaseName,
            string senderName,
            DateTimeOffset receivedAt,
            CancellationToken cancellationToken = default)
        {
            SuggestedBaseName = suggestedBaseName;
            return Task.FromResult<ProjectSelectionResult?>(null);
        }
    }
}
