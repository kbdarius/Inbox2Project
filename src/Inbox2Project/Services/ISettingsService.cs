using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed record CsvImportResult(int ImportedCount, int SkippedCount);

public interface ISettingsService
{
    Task<SettingsModel> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveLastSelectedProjectAsync(string projectPath, CancellationToken cancellationToken = default);

    Task SaveUseLocalAiFolderNamingAsync(bool useLocalAiFolderNaming, CancellationToken cancellationToken = default);

    Task<SavedProjectDefinition> AddProjectAsync(string projectName, string projectFolderPath, CancellationToken cancellationToken = default);

    Task<SavedProjectDefinition> EditProjectAsync(string originalProjectPath, string projectName, string projectFolderPath, CancellationToken cancellationToken = default);

    Task RemoveProjectAsync(string projectPath, CancellationToken cancellationToken = default);

    string DefaultBackupCsvPath { get; }

    Task<string> ExportSavedProjectsToCsvAsync(string csvPath, CancellationToken cancellationToken = default);

    Task<CsvImportResult> ImportSavedProjectsFromCsvAsync(string csvPath, CancellationToken cancellationToken = default);
}
