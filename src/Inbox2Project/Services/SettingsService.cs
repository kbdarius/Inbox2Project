using System.Text.Json;
using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;

    public SettingsService(string? appDataPath = null)
    {
        var appData = appDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsPath = Path.Combine(appData, "Inbox2Project", "settings.json");
    }

    public async Task<SettingsModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.CfgRootMissing);
            throw new AppException(AppErrorId.CfgRootMissing, userMessage, $"Settings file not found: {_settingsPath}");
        }

        var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
        var data = JsonSerializer.Deserialize<SettingsModel>(json, JsonOptions);
        if (data is null || string.IsNullOrWhiteSpace(data.ProjectsRoot))
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.CfgRootMissing);
            throw new AppException(AppErrorId.CfgRootMissing, userMessage, "ProjectsRoot is missing in settings file.");
        }

        if (!Directory.Exists(data.ProjectsRoot))
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.CfgRootInvalid);
            throw new AppException(AppErrorId.CfgRootInvalid, userMessage, $"ProjectsRoot does not exist: {data.ProjectsRoot}");
        }

        return data;
    }

    public async Task SaveLastSelectedProjectAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

        SettingsModel? existing = null;
        if (File.Exists(_settingsPath))
        {
            var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
            existing = JsonSerializer.Deserialize<SettingsModel>(json, JsonOptions);
        }

        var projectsRoot = existing?.ProjectsRoot ?? string.Empty;
        if (string.IsNullOrWhiteSpace(projectsRoot))
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.CfgRootMissing);
            throw new AppException(AppErrorId.CfgRootMissing, userMessage, "Cannot save LastSelectedProject because ProjectsRoot is missing.");
        }

        var model = new SettingsModel(projectsRoot, projectPath);
        var serialized = JsonSerializer.Serialize(model, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, serialized, cancellationToken);
    }
}
