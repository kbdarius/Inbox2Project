namespace Inbox2Project.Models;

public enum AiNamingProvider { None = 0, OpenAi = 1, GitHubModels = 2 }

public sealed class SettingsModel
{
	public string ProjectsRoot { get; set; } = string.Empty;

	public string? LastSelectedProject { get; set; }

	public List<SavedProjectDefinition> SavedProjects { get; set; } = new();

    /// <summary>Legacy field kept for migration only. Use <see cref="AiProvider"/> instead.</summary>
    public bool UseLocalAiFolderNaming { get; set; }

    public AiNamingProvider AiProvider { get; set; }
}

public sealed class SavedProjectDefinition
{
	public string Name { get; set; } = string.Empty;

	public string ProjectPath { get; set; } = string.Empty;
}
