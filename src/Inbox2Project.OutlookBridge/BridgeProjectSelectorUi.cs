using Inbox2Project.Models;
using Inbox2Project.Services;

namespace Inbox2Project.OutlookBridge;

public sealed class BridgeProjectSelectorUi : IProjectSelectorUi
{
    private readonly ISettingsService _settingsService;
    private readonly IAiFolderNameService _aiFolderNameService;

    public BridgeProjectSelectorUi(ISettingsService settingsService, IAiFolderNameService aiFolderNameService)
    {
        _settingsService = settingsService;
        _aiFolderNameService = aiFolderNameService;
    }

    public async Task<string> SelectProjectAsync(IReadOnlyList<string> projectPaths, string? suggestedProjectPath, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        using var form = new ProjectSelectorForm(_settingsService, projectPaths, settings, suggestedProjectPath, _aiFolderNameService);
        var result = form.ShowDialog();
        if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(form.SelectedProjectPath))
        {
            return form.SelectedProjectPath!;
        }

        return string.Empty;
    }
}
