using Inbox2Project.Models;
using Inbox2Project.Services;

namespace Inbox2Project.OutlookBridge;

public sealed class BridgeProjectSelectorUi : IProjectSelectorUi
{
    private readonly ISettingsService _settingsService;
    private readonly OpenAiFolderNameService _openAiService;
    private readonly GitHubModelsFolderNameService _gitHubModelsService;
    private readonly IPathSafetyService _pathSafetyService;

    public BridgeProjectSelectorUi(
        ISettingsService settingsService,
        OpenAiFolderNameService openAiService,
        GitHubModelsFolderNameService gitHubModelsService)
    {
        _settingsService = settingsService;
        _openAiService = openAiService;
        _gitHubModelsService = gitHubModelsService;
        _pathSafetyService = new PathSafetyService();
    }

    public async Task<ProjectSelectionResult?> SelectProjectAsync(
        IReadOnlyList<string> projectPaths,
        string? suggestedProjectPath,
        string suggestedBaseName,
        string senderName,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        using var form = new ProjectSelectorForm(_settingsService, _pathSafetyService, projectPaths, settings, suggestedProjectPath, suggestedBaseName, senderName, receivedAt, _openAiService, _gitHubModelsService);
        var result = form.ShowDialog();
        if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(form.SelectedProjectPath))
        {
            return new ProjectSelectionResult(form.SelectedProjectPath!, form.SelectedFinalName ?? suggestedBaseName, form.SelectedSaveAsMsg);
        }

        return null;
    }
}
