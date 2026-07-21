using System.Text;
using System.Text.Json;
using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _appRoot;
    private readonly string _settingsPath;
    private readonly string _defaultProjectsRoot;
    private readonly string _backupCsvPath;

    public SettingsService(string? appDataPath = null)
    {
        var appData = appDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _appRoot = Path.Combine(appData, "Inbox2Project");
        _settingsPath = Path.Combine(_appRoot, "settings.json");
        _defaultProjectsRoot = Path.Combine(_appRoot, "Projects");
        _backupCsvPath = Path.Combine(_appRoot, "saved-projects-backup.csv");
    }

    public string DefaultBackupCsvPath => _backupCsvPath;

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

    public async Task SaveUseLocalAiFolderNamingAsync(bool useLocalAiFolderNaming, CancellationToken cancellationToken = default)
    {
        var model = await LoadAsync(cancellationToken);
        model.UseLocalAiFolderNaming = useLocalAiFolderNaming;
        await SaveAsync(model, cancellationToken);
    }

    public async Task<SavedProjectDefinition> AddProjectAsync(string projectName, string projectFolderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectFolderPath))
        {
            throw new ArgumentException("Project folder path is required.", nameof(projectFolderPath));
        }

        var projectPath = Path.GetFullPath(projectFolderPath.Trim());
        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException("The project folder does not exist.");
        }

        var model = await LoadAsync(cancellationToken);
        var normalizedName = GetProjectName(projectName, projectPath);

        var existing = model.SavedProjects.FirstOrDefault(saved =>
            string.Equals(saved.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase));

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

    public async Task RemoveProjectAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        var model = await LoadAsync(cancellationToken);
        model.SavedProjects.RemoveAll(saved =>
            string.Equals(saved.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(model.LastSelectedProject, projectPath, StringComparison.OrdinalIgnoreCase))
        {
            model.LastSelectedProject = null;
        }

        await SaveAsync(model, cancellationToken);
    }

    private SettingsModel CreateDefaultSettings()
    {
        Directory.CreateDirectory(_defaultProjectsRoot);
        return new SettingsModel
        {
            ProjectsRoot = _defaultProjectsRoot,
            LastSelectedProject = null,
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
        settings.SavedProjects ??= new List<SavedProjectDefinition>();
        settings.SavedProjects = settings.SavedProjects
            .Where(project => !string.IsNullOrWhiteSpace(project.Name) && !string.IsNullOrWhiteSpace(project.ProjectPath))
            .Where(project => !IsBuiltInSample(project))
            .GroupBy(project => project.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (string.IsNullOrWhiteSpace(settings.LastSelectedProject)
            || !settings.SavedProjects.Any(project => string.Equals(project.ProjectPath, settings.LastSelectedProject, StringComparison.OrdinalIgnoreCase)))
        {
            settings.LastSelectedProject = null;
        }

        return settings;
    }

    public async Task<SavedProjectDefinition> EditProjectAsync(string originalProjectPath, string projectName, string projectFolderPath, CancellationToken cancellationToken = default)
    {
        var projectPath = Path.GetFullPath(projectFolderPath.Trim());
        if (!Directory.Exists(projectPath)) throw new DirectoryNotFoundException("The project folder does not exist.");

        var model = await LoadAsync(cancellationToken);
        var project = model.SavedProjects.FirstOrDefault(saved => string.Equals(saved.ProjectPath, originalProjectPath, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The project is no longer in the saved list.");
        project.Name = GetProjectName(projectName, projectPath);
        project.ProjectPath = projectPath;
        if (string.Equals(model.LastSelectedProject, originalProjectPath, StringComparison.OrdinalIgnoreCase)) model.LastSelectedProject = projectPath;
        await SaveAsync(model, cancellationToken);
        return project;
    }

    private static bool IsBuiltInSample(SavedProjectDefinition project)
    {
        var folderName = Path.GetFileName(project.ProjectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(project.Name, "Default", StringComparison.OrdinalIgnoreCase)
            || string.Equals(project.Name, "SampleProject", StringComparison.OrdinalIgnoreCase)
            || string.Equals(folderName, "Default", StringComparison.OrdinalIgnoreCase)
            || string.Equals(folderName, "SampleProject", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetProjectName(string? nickname, string projectPath)
    {
        if (!string.IsNullOrWhiteSpace(nickname)) return nickname.Trim();
        var directory = new DirectoryInfo(projectPath);
        if ((directory.Name.Equals("EMAIL", StringComparison.OrdinalIgnoreCase)
                || directory.Name.Equals("EMAILS", StringComparison.OrdinalIgnoreCase))
            && directory.Parent is not null)
        {
            return directory.Parent.Name;
        }

        return directory.Name;
    }

    private async Task SaveAsync(SettingsModel model, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var serialized = JsonSerializer.Serialize(model, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, serialized, cancellationToken);
        await WriteBackupCsvAsync(model.SavedProjects, cancellationToken);
    }

    private async Task WriteBackupCsvAsync(List<SavedProjectDefinition> savedProjects, CancellationToken cancellationToken)
    {
        // Never overwrite a good backup with an empty snapshot - protects against
        // settings.json being accidentally cleared/overwritten by another process.
        if (savedProjects.Count == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_appRoot);
            var lines = new List<string> { "Name,ProjectPath" };
            lines.AddRange(savedProjects.Select(project => $"{CsvEscape(project.Name)},{CsvEscape(project.ProjectPath)}"));
            await File.WriteAllLinesAsync(_backupCsvPath, lines, cancellationToken);
        }
        catch
        {
            // Backup is best-effort; never let a backup failure block the primary save.
        }
    }

    public async Task<string> ExportSavedProjectsToCsvAsync(string csvPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            throw new ArgumentException("A destination CSV path is required.", nameof(csvPath));
        }

        var model = await LoadAsync(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(csvPath))!);
        var lines = new List<string> { "Name,ProjectPath" };
        lines.AddRange(model.SavedProjects.Select(project => $"{CsvEscape(project.Name)},{CsvEscape(project.ProjectPath)}"));
        await File.WriteAllLinesAsync(csvPath, lines, cancellationToken);
        return csvPath;
    }

    public async Task<CsvImportResult> ImportSavedProjectsFromCsvAsync(string csvPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException("The backup CSV file was not found.", csvPath);
        }

        var lines = await File.ReadAllLinesAsync(csvPath, cancellationToken);
        var model = await LoadAsync(cancellationToken);
        var imported = 0;
        var skipped = 0;

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            if (fields.Count < 2)
            {
                skipped++;
                continue;
            }

            var name = fields[0].Trim();
            var projectPath = fields[1].Trim();
            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            {
                skipped++;
                continue;
            }

            var existing = model.SavedProjects.FirstOrDefault(saved =>
                string.Equals(saved.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                skipped++;
                continue;
            }

            model.SavedProjects.Add(new SavedProjectDefinition
            {
                Name = string.IsNullOrWhiteSpace(name) ? GetProjectName(null, projectPath) : name,
                ProjectPath = projectPath,
            });
            imported++;
        }

        await SaveAsync(model, cancellationToken);
        return new CsvImportResult(imported, skipped);
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
