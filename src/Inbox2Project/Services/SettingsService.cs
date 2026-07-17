using System.Text.Json;
using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _appRoot;
    private readonly string _settingsPath;
    private readonly string _defaultProjectsRoot;
    private readonly string _defaultProjectPath;

    public SettingsService(string? appDataPath = null)
    {
        var appData = appDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _appRoot = Path.Combine(appData, "Inbox2Project");
        _settingsPath = Path.Combine(_appRoot, "settings.json");
        _defaultProjectsRoot = Path.Combine(_appRoot, "Projects");
        _defaultProjectPath = Path.Combine(_defaultProjectsRoot, "Default");
    }

    public async Task<SettingsModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            var created = CreateDefaultSettings();
            await SaveAsync(created, cancellationToken);
            return created;
        }

        var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
        var data = JsonSerializer.Deserialize<SettingsModel>(json, JsonOptions);
        if (data is null)
        {
            data = CreateDefaultSettings();
            await SaveAsync(data, cancellationToken);
            return data;
        }

        var normalized = Normalize(data);
        await SaveAsync(normalized, cancellationToken);
        return normalized;
    }

    public async Task SaveLastSelectedProjectAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        var model = await LoadAsync(cancellationToken);
        model.LastSelectedProject = projectPath;
        await SaveAsync(model, cancellationToken);
    }

    public async Task<SavedProjectDefinition> AddProjectAsync(string projectName, string parentFolderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new ArgumentException("Project name is required.", nameof(projectName));
        }

        if (string.IsNullOrWhiteSpace(parentFolderPath))
        {
            throw new ArgumentException("Parent folder path is required.", nameof(parentFolderPath));
        }

        Directory.CreateDirectory(parentFolderPath);

        var model = await LoadAsync(cancellationToken);
        var normalizedName = projectName.Trim();
        var projectPath = Path.Combine(parentFolderPath, normalizedName);
        Directory.CreateDirectory(Path.Combine(projectPath, "EMAILS"));

        var existing = model.SavedProjects.FirstOrDefault(saved =>
            string.Equals(saved.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(saved.Name, normalizedName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            existing = new SavedProjectDefinition();
            model.SavedProjects.Add(existing);
        }

        existing.Name = normalizedName;
        existing.ProjectPath = projectPath;
        model.LastSelectedProject = projectPath;

        await SaveAsync(model, cancellationToken);
        return existing;
    }

    private SettingsModel CreateDefaultSettings()
    {
        Directory.CreateDirectory(Path.Combine(_defaultProjectPath, "EMAILS"));
        return new SettingsModel
        {
            ProjectsRoot = _defaultProjectsRoot,
            LastSelectedProject = _defaultProjectPath,
            SavedProjects = new List<SavedProjectDefinition>(),
        };
    }

    private SettingsModel Normalize(SettingsModel settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ProjectsRoot))
        {
            settings.ProjectsRoot = _defaultProjectsRoot;
        }

        Directory.CreateDirectory(settings.ProjectsRoot);
        Directory.CreateDirectory(Path.Combine(_defaultProjectPath, "EMAILS"));

        settings.SavedProjects ??= new List<SavedProjectDefinition>();
        settings.SavedProjects = settings.SavedProjects
            .Where(project => !string.IsNullOrWhiteSpace(project.Name) && !string.IsNullOrWhiteSpace(project.ProjectPath))
            .GroupBy(project => project.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (string.IsNullOrWhiteSpace(settings.LastSelectedProject) || !Directory.Exists(Path.Combine(settings.LastSelectedProject, "EMAILS")))
        {
            settings.LastSelectedProject = _defaultProjectPath;
        }

        return settings;
    }

    private async Task SaveAsync(SettingsModel model, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var serialized = JsonSerializer.Serialize(model, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, serialized, cancellationToken);
    }
}
