using System.Text;
using System.Text.Json;
using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly SharedProjectArchive[] SharedProjectArchives =
    [
        new("BESS", ["DOCs", "07_Meetings_Emails_Chats", "EMAILS"]),
        new("CASES", ["DOCs", "03_Meetings_Emails_Chats", "EMAILS"]),
        new("DIU", ["DOCs", "05_Meetings_Emails_Chats", "EMAILS"]),
        new("Li610", ["DOCs", "09_Meetings_Emails_Chats", "EMAILS"]),
        new("NexusGateway", ["01_Meetings_Emails_Chats", "EMAILS"]),
    ];

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
            var created = Normalize(CreateDefaultSettings());
            await SaveAsync(created, cancellationToken);
            return created;
        }

        var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
        var data = JsonSerializer.Deserialize<SettingsModel>(json, JsonOptions);
        if (data is null)
        {
            data = Normalize(CreateDefaultSettings());
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
        await SaveAiProviderAsync(useLocalAiFolderNaming ? AiNamingProvider.OpenAi : AiNamingProvider.None, cancellationToken);
    }

    public async Task SaveAiProviderAsync(AiNamingProvider provider, CancellationToken cancellationToken = default)
    {
        var model = await LoadAsync(cancellationToken);
        model.AiProvider = provider;
        model.UseLocalAiFolderNaming = provider == AiNamingProvider.OpenAi;
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

    public async Task<SavedProjectDefinition> AddSubprojectAsync(
        string mainProjectPath,
        string subprojectName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subprojectName))
        {
            throw new ArgumentException("A subproject name is required.", nameof(subprojectName));
        }

        var model = await LoadAsync(cancellationToken);
        var mainProject = model.SavedProjects.FirstOrDefault(saved =>
            string.Equals(saved.ProjectPath, mainProjectPath, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The main project is no longer in the saved list.");
        var normalizedName = subprojectName.Trim();
        if (string.Equals(normalizedName, mainProject.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A subproject name must be different from its main project.", nameof(subprojectName));
        }

        mainProject.Subprojects ??= new List<string>();
        if (!mainProject.Subprojects.Any(name => string.Equals(name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            mainProject.Subprojects.Add(normalizedName);
            mainProject.Subprojects = NormalizeSubprojects(mainProject.Subprojects);
            await SaveAsync(model, cancellationToken);
        }

        return mainProject;
    }

    public async Task RemoveSubprojectAsync(
        string mainProjectPath,
        string subprojectName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subprojectName))
        {
            return;
        }

        var model = await LoadAsync(cancellationToken);
        var mainProject = model.SavedProjects.FirstOrDefault(saved =>
            string.Equals(saved.ProjectPath, mainProjectPath, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The main project is no longer in the saved list.");
        mainProject.Subprojects ??= new List<string>();
        mainProject.Subprojects.RemoveAll(name => string.Equals(name, subprojectName.Trim(), StringComparison.OrdinalIgnoreCase));
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
        var normalizedProjects = settings.SavedProjects
            .Where(project => !string.IsNullOrWhiteSpace(project.Name) && !string.IsNullOrWhiteSpace(project.ProjectPath))
            .Where(project => !IsBuiltInSample(project))
            .Select(project => NormalizeSavedProject(project))
            .GroupBy(project => project.Project.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var mainProjectName = group.Select(item => item.MainProjectName).FirstOrDefault(name => name is not null);
                return new SavedProjectDefinition
                {
                    Name = mainProjectName ?? group.First().Project.Name,
                    ProjectPath = group.First().Project.ProjectPath,
                    Subprojects = NormalizeSubprojects(group.SelectMany(item => item.Project.Subprojects)),
                };
            })
            .ToList();

        var codeRoot = FindSharedCodeRoot(settings.SavedProjects, settings.SavedProjects.Count == 0);
        if (!settings.SharedProjectArchivesInitialized && codeRoot is not null)
        {
            foreach (var archive in SharedProjectArchives)
            {
                var archivePath = GetArchivePath(codeRoot, archive);
                if (!normalizedProjects.Any(project => string.Equals(project.ProjectPath, archivePath, StringComparison.OrdinalIgnoreCase)))
                {
                    normalizedProjects.Add(new SavedProjectDefinition
                    {
                        Name = archive.ProjectName,
                        ProjectPath = archivePath,
                    });
                }
            }

            settings.SharedProjectArchivesInitialized = true;
        }

        settings.SavedProjects = normalizedProjects;

        if (!string.IsNullOrWhiteSpace(settings.LastSelectedProject)
            && TryGetSharedArchiveLocation(settings.LastSelectedProject, out var lastSelectedArchive))
        {
            settings.LastSelectedProject = lastSelectedArchive.CanonicalPath;
        }

        if (string.IsNullOrWhiteSpace(settings.LastSelectedProject)
            || !settings.SavedProjects.Any(project => string.Equals(project.ProjectPath, settings.LastSelectedProject, StringComparison.OrdinalIgnoreCase)))
        {
            settings.LastSelectedProject = null;
        }

        // GitHub Models support was retired. Move existing users to OpenAI so
        // their AI naming preference remains enabled after upgrading.
        if ((int)settings.AiProvider == 2)
        {
            settings.AiProvider = AiNamingProvider.OpenAi;
        }

        // Migrate legacy UseLocalAiFolderNaming -> AiProvider
        if (settings.AiProvider == AiNamingProvider.None && settings.UseLocalAiFolderNaming)
        {
            settings.AiProvider = AiNamingProvider.OpenAi;
        }
        settings.UseLocalAiFolderNaming = settings.AiProvider == AiNamingProvider.OpenAi;

        return settings;
    }

    private static NormalizedSavedProject NormalizeSavedProject(SavedProjectDefinition project)
    {
        var subprojects = NormalizeSubprojects(project.Subprojects ?? new List<string>());
        if (!TryGetSharedArchiveLocation(project.ProjectPath, out var archive))
        {
            return new NormalizedSavedProject(
                new SavedProjectDefinition
                {
                    Name = project.Name.Trim(),
                    ProjectPath = project.ProjectPath,
                    Subprojects = subprojects,
                },
                null);
        }

        if (!IsLegacyMainArchiveNickname(project.Name, archive.ProjectName))
        {
            subprojects = NormalizeSubprojects(subprojects.Append(project.Name));
        }

        return new NormalizedSavedProject(
            new SavedProjectDefinition
            {
                Name = archive.ProjectName,
                ProjectPath = archive.CanonicalPath,
                Subprojects = subprojects,
            },
            archive.ProjectName);
    }

    private static List<string> NormalizeSubprojects(IEnumerable<string?> subprojects)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>();
        foreach (var subproject in subprojects)
        {
            var name = subproject?.Trim();
            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
            {
                normalized.Add(name);
            }
        }

        return normalized;
    }

    private static bool IsLegacyMainArchiveNickname(string name, string mainProjectName)
    {
        if (string.Equals(name.Trim(), mainProjectName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (mainProjectName.Equals("NexusGateway", StringComparison.OrdinalIgnoreCase)
            && name.Equals("NexusGateway-Digi", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!name.StartsWith(mainProjectName + "_", StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith(mainProjectName + "-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = name[(mainProjectName.Length + 1)..];
        var digitCount = suffix.TakeWhile(char.IsDigit).Count();
        if (digitCount is < 1 or > 2 || digitCount >= suffix.Length || suffix[digitCount] is not ('_' or '-'))
        {
            return false;
        }

        var category = suffix[(digitCount + 1)..];
        return category.Contains("meeting", StringComparison.OrdinalIgnoreCase)
            || category.Contains("email", StringComparison.OrdinalIgnoreCase)
            || category.Contains("chat", StringComparison.OrdinalIgnoreCase)
            || category.Contains("transcript", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindSharedCodeRoot(IEnumerable<SavedProjectDefinition> projects, bool useProfileFallback)
    {
        foreach (var project in projects)
        {
            if (TryGetCodeRoot(project.ProjectPath, out var codeRoot, out _))
            {
                return codeRoot;
            }
        }

        if (!useProfileFallback)
        {
            return null;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(userProfile, "OneDrive - stryten.com"),
            Environment.GetEnvironmentVariable("OneDriveCommercial"),
            Environment.GetEnvironmentVariable("OneDrive"),
        };

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path));
    }

    private static bool TryGetSharedArchiveLocation(string path, out SharedArchiveLocation location)
    {
        location = null!;
        if (!TryGetCodeRoot(path, out var codeRoot, out var projectName) || projectName is null)
        {
            return false;
        }

        var archive = SharedProjectArchives.FirstOrDefault(item =>
            string.Equals(item.ProjectName, projectName, StringComparison.OrdinalIgnoreCase));
        if (archive is null)
        {
            return false;
        }

        location = new SharedArchiveLocation(archive.ProjectName, codeRoot, GetArchivePath(codeRoot, archive));
        return true;
    }

    private static bool TryGetCodeRoot(string path, out string codeRoot, out string? projectName)
    {
        codeRoot = string.Empty;
        projectName = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }

        var segments = fullPath[root.Length..]
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var codeIndex = Array.FindIndex(segments, segment => string.Equals(segment, "Code", StringComparison.OrdinalIgnoreCase));
        if (codeIndex < 0)
        {
            return false;
        }

        codeRoot = root;
        for (var i = 0; i < codeIndex; i++)
        {
            codeRoot = Path.Combine(codeRoot, segments[i]);
        }

        if (codeIndex + 1 < segments.Length)
        {
            projectName = segments[codeIndex + 1];
        }

        return true;
    }

    private static string GetArchivePath(string codeRoot, SharedProjectArchive archive)
    {
        var path = Path.Combine(codeRoot, "Code", archive.ProjectName);
        foreach (var segment in archive.RelativePath)
        {
            path = Path.Combine(path, segment);
        }

        return Path.GetFullPath(path);
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
        if (TryGetSharedArchiveLocation(projectPath, out var archive)
            && string.Equals(Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "EMAILS", StringComparison.OrdinalIgnoreCase))
        {
            return archive.ProjectName;
        }

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
            var lines = new List<string> { "Name,ProjectPath,SubprojectsJson" };
            lines.AddRange(savedProjects.Select(project =>
                $"{CsvEscape(project.Name)},{CsvEscape(project.ProjectPath)},{CsvEscape(JsonSerializer.Serialize(project.Subprojects ?? new List<string>()))}"));
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
        var lines = new List<string> { "Name,ProjectPath,SubprojectsJson" };
        lines.AddRange(model.SavedProjects.Select(project =>
            $"{CsvEscape(project.Name)},{CsvEscape(project.ProjectPath)},{CsvEscape(JsonSerializer.Serialize(project.Subprojects ?? new List<string>()))}"));
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
            var importedSubprojects = fields.Count >= 3
                ? DeserializeSubprojects(fields[2])
                : new List<string>();
            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            {
                skipped++;
                continue;
            }

            var existing = model.SavedProjects.FirstOrDefault(saved =>
                string.Equals(saved.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Subprojects = NormalizeSubprojects(existing.Subprojects.Concat(importedSubprojects));
                skipped++;
                continue;
            }

            model.SavedProjects.Add(new SavedProjectDefinition
            {
                Name = string.IsNullOrWhiteSpace(name) ? GetProjectName(null, projectPath) : name,
                ProjectPath = projectPath,
                Subprojects = importedSubprojects,
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

    private static List<string> DeserializeSubprojects(string json)
    {
        try
        {
            return NormalizeSubprojects(JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>());
        }
        catch (JsonException)
        {
            return new List<string>();
        }
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

    private sealed record SharedProjectArchive(string ProjectName, string[] RelativePath);

    private sealed record SharedArchiveLocation(string ProjectName, string CodeRoot, string CanonicalPath);

    private sealed record NormalizedSavedProject(SavedProjectDefinition Project, string? MainProjectName);
}
