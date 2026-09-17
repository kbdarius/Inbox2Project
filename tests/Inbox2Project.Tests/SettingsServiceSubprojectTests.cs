using System.Text.Json;
using Inbox2Project.Models;
using Inbox2Project.Services;
using Xunit;

namespace Inbox2Project.Tests;

public sealed class SettingsServiceSubprojectTests
{
    [Fact]
    public async Task LoadAsync_MigratesLegacyProjectFoldersToSharedArchiveAndSubprojects()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Inbox2Project.Tests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(tempRoot, "AppData");
        var codeRoot = Path.Combine(tempRoot, "OneDrive - stryten.com");
        Directory.CreateDirectory(Path.Combine(appDataPath, "Inbox2Project"));
        Directory.CreateDirectory(codeRoot);

        var opusPath = Path.Combine(codeRoot, "Code", "Li610", "OPUS", "DOCs", "MEETINGS & EMAILS");
        var ulPath = Path.Combine(codeRoot, "Code", "Li610", "DOCs", "10_PROJECTS", "UL", "MEETING_EMAILS");
        var chargerPath = Path.Combine(codeRoot, "Code", "Li610", "DOCs", "10_PROJECTS", "Charger as a Gateway", "Meeting", "EMAILS");
        var forgeNanoPath = Path.Combine(codeRoot, "Code", "Li610", "DOCs", "10_PROJECTS", "Forge Nano Build", "MEETING & EMAILS");
        var dbcAutoPath = Path.Combine(codeRoot, "Code", "Li610", "DOCs", "10_PROJECTS", "DBC-AUTO");
        var tbTesterPath = Path.Combine(codeRoot, "Code", "Li610", "DOCs", "10_PROJECTS", "TB_TESTER");
        var bedOfNailsPath = Path.Combine(codeRoot, "Code", "Li610", "DOCs", "10_PROJECTS", "BedofNails_EOL_Tester", "EMAILS");
        var oldLi610Archive = Path.Combine(codeRoot, "Code", "Li610", "DOCs", "09_Meetings_Emails", "EMAILS");
        var oldBessArchive = Path.Combine(codeRoot, "Code", "BESS", "DOCs", "07_Meeting_Transcripts_Emails", "EMAILS");
        var oldDiuArchive = Path.Combine(codeRoot, "Code", "DIU", "DOCs", "Emails");
        var oldNexusArchive = Path.Combine(codeRoot, "Code", "NexusGateway", "01-Meetings & Emails");
        var cicdPath = Path.Combine(codeRoot, "Code", "Other", "CICD", "EMAILS");

        var initialSettings = new SettingsModel
        {
            ProjectsRoot = Path.Combine(appDataPath, "Inbox2Project", "Projects"),
            LastSelectedProject = opusPath,
            SavedProjects =
            [
                new SavedProjectDefinition { Name = "OPUS", ProjectPath = opusPath },
                new SavedProjectDefinition { Name = "UL", ProjectPath = ulPath },
                new SavedProjectDefinition { Name = "Charger as a Gateway", ProjectPath = chargerPath },
                new SavedProjectDefinition { Name = "Forge Nano", ProjectPath = forgeNanoPath },
                new SavedProjectDefinition { Name = "DBC-AUTO", ProjectPath = dbcAutoPath },
                new SavedProjectDefinition { Name = "TB_TESTER", ProjectPath = tbTesterPath },
                new SavedProjectDefinition { Name = "BedofNails_EOL_Tester", ProjectPath = bedOfNailsPath },
                new SavedProjectDefinition { Name = "Li610_09_Meetings_Emails", ProjectPath = oldLi610Archive },
                new SavedProjectDefinition { Name = "BESS_07_Meeting_Transcripts_Emails", ProjectPath = oldBessArchive },
                new SavedProjectDefinition { Name = "DIU", ProjectPath = oldDiuArchive },
                new SavedProjectDefinition { Name = "NexusGateway-Digi", ProjectPath = oldNexusArchive },
                new SavedProjectDefinition { Name = "CICD", ProjectPath = cicdPath },
            ],
        };
        await File.WriteAllTextAsync(
            Path.Combine(appDataPath, "Inbox2Project", "settings.json"),
            JsonSerializer.Serialize(initialSettings));

        try
        {
            var service = new SettingsService(appDataPath);
            var migrated = await service.LoadAsync();
            var li610Archive = Path.Combine(codeRoot, "Code", "Li610", "DOCs", "09_Meetings_Emails_Chats", "EMAILS");
            var bessArchive = Path.Combine(codeRoot, "Code", "BESS", "DOCs", "07_Meetings_Emails_Chats", "EMAILS");

            var li610 = Assert.Single(migrated.SavedProjects, project =>
                string.Equals(project.ProjectPath, li610Archive, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(
                new[] { "BedofNails_EOL_Tester", "Charger as a Gateway", "DBC-AUTO", "Forge Nano", "OPUS", "TB_TESTER", "UL" },
                li610.Subprojects.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            Assert.Contains(migrated.SavedProjects, project =>
                string.Equals(project.Name, "BESS", StringComparison.Ordinal)
                && string.Equals(project.ProjectPath, bessArchive, StringComparison.OrdinalIgnoreCase)
                && project.Subprojects.Count == 0);
            Assert.Contains(migrated.SavedProjects, project =>
                string.Equals(project.Name, "CICD", StringComparison.Ordinal)
                && string.Equals(project.ProjectPath, cicdPath, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(migrated.SavedProjects, project =>
                project.Name == "NexusGateway" && project.Subprojects.Count == 0);
            Assert.Equal(li610Archive, migrated.LastSelectedProject);
            Assert.Equal(new[] { "BESS", "CASES", "DIU", "Li610", "NexusGateway" },
                migrated.SavedProjects.Where(project => project.Name is "BESS" or "CASES" or "DIU" or "Li610" or "NexusGateway")
                    .Select(project => project.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

            var secondLoad = await service.LoadAsync();
            var secondLi610 = Assert.Single(secondLoad.SavedProjects, project =>
                string.Equals(project.ProjectPath, li610Archive, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(7, secondLi610.Subprojects.Count);
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
    public async Task Subprojects_CanBeManagedWithoutFoldersAndRoundTripThroughCsv()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Inbox2Project.Tests", Guid.NewGuid().ToString("N"));
        var appDataPath = Path.Combine(tempRoot, "AppData");
        var codeRoot = Path.Combine(tempRoot, "OneDrive - stryten.com");
        var settingsPath = Path.Combine(appDataPath, "Inbox2Project", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        Directory.CreateDirectory(codeRoot);
        var li610Path = Path.Combine(codeRoot, "Code", "Li610", "OPUS", "DOCs", "EMAILS");
        await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(new SettingsModel
        {
            ProjectsRoot = Path.Combine(appDataPath, "Inbox2Project", "Projects"),
            SavedProjects = [new SavedProjectDefinition { Name = "OPUS", ProjectPath = li610Path }],
        }));

        try
        {
            var service = new SettingsService(appDataPath);
            var migrated = await service.LoadAsync();
            var li610 = Assert.Single(migrated.SavedProjects, project => project.Name == "Li610");
            var canonicalPath = li610.ProjectPath;
            Assert.False(Directory.Exists(canonicalPath));

            await service.AddSubprojectAsync(canonicalPath, "UL");
            await service.AddSubprojectAsync(canonicalPath, "ul");
            var withSubproject = await service.LoadAsync();
            li610 = Assert.Single(withSubproject.SavedProjects, project => project.Name == "Li610");
            Assert.Equal(new[] { "OPUS", "UL" }, li610.Subprojects.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

            Directory.CreateDirectory(canonicalPath);
            var csvPath = Path.Combine(tempRoot, "projects.csv");
            await service.ExportSavedProjectsToCsvAsync(csvPath);
            Assert.Contains("SubprojectsJson", await File.ReadAllTextAsync(csvPath));

            var importedAppData = Path.Combine(tempRoot, "ImportedAppData");
            var importedService = new SettingsService(importedAppData);
            await importedService.ImportSavedProjectsFromCsvAsync(csvPath);
            var importedSettings = await importedService.LoadAsync();
            var importedLi610 = Assert.Single(importedSettings.SavedProjects, project =>
                string.Equals(project.ProjectPath, canonicalPath, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(new[] { "OPUS", "UL" }, importedLi610.Subprojects.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

            await service.RemoveSubprojectAsync(canonicalPath, "opus");
            var afterRemoval = await service.LoadAsync();
            li610 = Assert.Single(afterRemoval.SavedProjects, project => project.Name == "Li610");
            Assert.Equal(new[] { "UL" }, li610.Subprojects);
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
