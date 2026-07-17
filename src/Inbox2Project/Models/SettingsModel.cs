namespace Inbox2Project.Models;

public sealed class SettingsModel
{
	public string ProjectsRoot { get; set; } = string.Empty;

	public string? LastSelectedProject { get; set; }

	public List<SavedProjectDefinition> SavedProjects { get; set; } = new();
}

public sealed class SavedProjectDefinition
{
	public string Name { get; set; } = string.Empty;

	public string ProjectPath { get; set; } = string.Empty;
}
