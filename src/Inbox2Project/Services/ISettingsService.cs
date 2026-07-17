using Inbox2Project.Models;

namespace Inbox2Project.Services;

public interface ISettingsService
{
    Task<SettingsModel> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveLastSelectedProjectAsync(string projectPath, CancellationToken cancellationToken = default);

    Task<SavedProjectDefinition> AddProjectAsync(string projectName, string parentFolderPath, CancellationToken cancellationToken = default);
}
