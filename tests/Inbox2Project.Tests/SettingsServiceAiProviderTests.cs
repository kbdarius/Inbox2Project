using Inbox2Project.Models;
using Inbox2Project.Services;
using Xunit;

namespace Inbox2Project.Tests;

public sealed class SettingsServiceAiProviderTests
{
    [Fact]
    public async Task SaveAiProviderAsync_CopilotRoundTripsWithoutEnablingLegacyLocalAi()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Inbox2Project.Tests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(tempRoot, "AppData");

        try
        {
            var service = new SettingsService(appDataPath);
            await service.SaveAiProviderAsync(AiNamingProvider.Copilot);

            var settings = await service.LoadAsync();

            Assert.Equal(AiNamingProvider.Copilot, settings.AiProvider);
            Assert.False(settings.UseLocalAiFolderNaming);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_ContinuesMigratingRetiredProviderValueTwoToOpenAi()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Inbox2Project.Tests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(tempRoot, "AppData");
        var settingsDirectory = Path.Combine(appDataPath, "Inbox2Project");
        Directory.CreateDirectory(settingsDirectory);

        try
        {
            var settings = new SettingsModel
            {
                ProjectsRoot = Path.Combine(settingsDirectory, "Projects"),
                AiProvider = (AiNamingProvider)2,
            };
            await File.WriteAllTextAsync(
                Path.Combine(settingsDirectory, "settings.json"),
                System.Text.Json.JsonSerializer.Serialize(settings));

            var loaded = await new SettingsService(appDataPath).LoadAsync();

            Assert.Equal(AiNamingProvider.OpenAi, loaded.AiProvider);
            Assert.True(loaded.UseLocalAiFolderNaming);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
