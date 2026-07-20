using Inbox2Project.Models;
using Inbox2Project.Services;
using Button = System.Windows.Forms.Button;
using CheckBox = System.Windows.Forms.CheckBox;
using ComboBox = System.Windows.Forms.ComboBox;
using DialogResult = System.Windows.Forms.DialogResult;
using LinkLabel = System.Windows.Forms.LinkLabel;
using Form = System.Windows.Forms.Form;
using Label = System.Windows.Forms.Label;
using ListBox = System.Windows.Forms.ListBox;
using MessageBox = System.Windows.Forms.MessageBox;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using TabControl = System.Windows.Forms.TabControl;
using TabPage = System.Windows.Forms.TabPage;
using TextBox = System.Windows.Forms.TextBox;

namespace Inbox2Project.OutlookBridge;

internal sealed class ProjectSelectorForm : Form
{
    private readonly ISettingsService _settingsService;
    private readonly List<ProjectOption> _projects;
    private readonly TabControl _tabs;
    private readonly IAiFolderNameService _aiFolderNameService;

    private readonly ComboBox _projectCombo;
    private readonly Label _selectedPathLabel;
    private readonly Button _saveButton;
    private readonly CheckBox _useLocalAiCheck;
    private readonly Label _aiStatusLabel;
    private readonly LinkLabel _aiSetupLink;
    private readonly TextBox _projectNameTextBox;
    private readonly TextBox _parentFolderTextBox;
    private readonly Button _addButton;
    private readonly Label _addStatusLabel;
    private readonly ListBox _removeListBox;
    private readonly Label _removeStatusLabel;
    private string? _editingProjectPath;

    public ProjectSelectorForm(
        ISettingsService settingsService,
        IReadOnlyList<string> projectPaths,
        SettingsModel settings,
        string? suggestedProjectPath,
        IAiFolderNameService aiFolderNameService)
    {
        _settingsService = settingsService;
        _aiFolderNameService = aiFolderNameService;
        _projects = BuildProjectOptions(projectPaths, settings.SavedProjects, settings.LastSelectedProject);

        Text = AppInfo.WindowTitle("Select Project");
        Width = 640;
        Height = 380;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new System.Drawing.Font("Segoe UI", 9F);
        BackColor = System.Drawing.Color.White;

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
        };

        var selectTab = new TabPage("Select Project");
        var addTab = new TabPage("Add Project");

        _projectCombo = new ComboBox
        {
            Left = 20,
            Top = 32,
            Width = 540,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _projectCombo.SelectedIndexChanged += (_, _) => UpdatePathLabel();
        _projectCombo.DoubleClick += (_, _) => ConfirmSelection();

        _selectedPathLabel = new Label
        {
            Left = 20,
            Top = 184,
            Width = 540,
            Height = 48,
        };

        _saveButton = new Button
        {
            Left = 380,
            Top = 250,
            Width = 180,
            Height = 36,
            Text = "Save to Selected Project",
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
        };
        _saveButton.Click += (_, _) => ConfirmSelection();

        _useLocalAiCheck = new CheckBox
        {
            Left = 20,
            Top = 96,
            Width = 540,
            Text = "Use local AI folder naming (Ollama)",
            Checked = settings.UseLocalAiFolderNaming,
        };
        _useLocalAiCheck.CheckedChanged += async (_, _) => await UpdateAiOptionAsync();

        _aiStatusLabel = new Label
        {
            Left = 20,
            Top = 122,
            Width = 540,
            Height = 44,
            Text = "Checking AI setup...",
        };

        _aiSetupLink = new LinkLabel
        {
            Left = 20,
            Top = 168,
            Width = 540,
            Height = 28,
            Text = $"Install Ollama or view model setup guide: {_aiFolderNameService.DownloadUrl}",
            Visible = false,
        };
        _aiSetupLink.Links.Add(0, _aiSetupLink.Text.Length, _aiFolderNameService.DownloadUrl);
        _aiSetupLink.LinkClicked += (_, args) =>
        {
            if (args.Link?.LinkData is string uri && System.Uri.TryCreate(uri, System.UriKind.Absolute, out var url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.ToString()) { UseShellExecute = true });
            }
        };

        selectTab.Controls.Add(new Label { Left = 20, Top = 12, Width = 200, Text = "Select project to save into:" });
        selectTab.Controls.Add(_projectCombo);
        selectTab.Controls.Add(_useLocalAiCheck);
        selectTab.Controls.Add(_aiStatusLabel);
        selectTab.Controls.Add(_aiSetupLink);
        selectTab.Controls.Add(_selectedPathLabel);
        selectTab.Controls.Add(new Label { Left = 20, Top = 198, Width = 540, Height = 32, Text = "Tip: add a new project on the Add Project tab first, then return here to select it." });
        selectTab.Controls.Add(_saveButton);

        _parentFolderTextBox = new TextBox
        {
            Left = 20,
            Top = 32,
            Width = 540,
        };
        _parentFolderTextBox.TextChanged += (_, _) => UpdateLocationAndNicknameState();

        _projectNameTextBox = new TextBox
        {
            Left = 20,
            Top = 100,
            Width = 540,
            Enabled = false,
        };
        _parentFolderTextBox.TextChanged += (_, _) => UpdateAddButtonState();
        _projectNameTextBox.TextChanged += (_, _) => UpdateAddButtonState();

        _addButton = new Button
        {
            Left = 380,
            Top = 200,
            Width = 180,
            Height = 36,
            Text = "Add Project",
        };
        _addButton.Click += async (_, _) => await AddProjectAsync();

        _addStatusLabel = new Label
        {
            Left = 20,
            Top = 244,
            Width = 540,
            Height = 32,
            ForeColor = System.Drawing.Color.DarkGreen,
        };

        addTab.Controls.Add(new Label { Left = 20, Top = 12, Width = 300, Text = "Destination Folder (must already exist)" });
        addTab.Controls.Add(_parentFolderTextBox);
        addTab.Controls.Add(new Label { Left = 20, Top = 80, Width = 260, Text = "Nickname (optional)" });
        addTab.Controls.Add(_projectNameTextBox);
        addTab.Controls.Add(new Label { Left = 20, Top = 132, Width = 540, Text = "Files are saved directly to this location. The nickname is suggested from the folder name." });
        addTab.Controls.Add(_addButton);
        addTab.Controls.Add(_addStatusLabel);

        // ── Manage tab ──────────────────────────────────────────────
        var manageTab = new TabPage("Manage Projects");

        _removeListBox = new ListBox
        {
            Left = 20,
            Top = 32,
            Width = 540,
            Height = 140,
            SelectionMode = SelectionMode.One,
        };

        var removeButton = new Button
        {
            Left = 380,
            Top = 184,
            Width = 180,
            Height = 36,
            Text = "Remove Selected",
        };
        removeButton.Click += async (_, _) => await RemoveProjectAsync();

        var editButton = new Button
        {
            Left = 230,
            Top = 184,
            Width = 140,
            Height = 36,
            Text = "Edit Selected",
        };
        editButton.Click += (_, _) => BeginEditProject();

        _removeStatusLabel = new Label
        {
            Left = 20,
            Top = 230,
            Width = 540,
            Height = 32,
            ForeColor = System.Drawing.Color.DarkGreen,
        };

        manageTab.Controls.Add(new Label { Left = 20, Top = 12, Width = 540, Text = "Saved projects:" });
        manageTab.Controls.Add(_removeListBox);
        manageTab.Controls.Add(new Label { Left = 20, Top = 190, Width = 200, Height = 24, Text = "List only; files are kept." });
        manageTab.Controls.Add(removeButton);
        manageTab.Controls.Add(editButton);
        manageTab.Controls.Add(_removeStatusLabel);

        _tabs.Controls.Add(selectTab);
        _tabs.Controls.Add(addTab);
        _tabs.Controls.Add(manageTab);
        Controls.Add(_tabs);

        PopulateProjects(suggestedProjectPath);
        PopulateRemoveList(settings.SavedProjects);
        UpdateAddButtonState();
        Load += async (_, _) => await UpdateAiStatusAsync();
    }

    public string? SelectedProjectPath { get; private set; }

    private static List<ProjectOption> BuildProjectOptions(
        IReadOnlyList<string> projectPaths,
        IReadOnlyList<SavedProjectDefinition> savedProjects,
        string? lastSelectedProject)
    {
        var savedByPath = savedProjects
            .GroupBy(saved => saved.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return projectPaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var displayName = savedByPath.TryGetValue(path, out var saved)
                    ? saved.Name
                    : Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                return new ProjectOption(displayName, path);
            })
            .OrderByDescending(option => string.Equals(option.Path, lastSelectedProject, StringComparison.OrdinalIgnoreCase))
            .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void PopulateProjects(string? suggestedProjectPath)
    {
        _projectCombo.Items.Clear();
        foreach (var project in _projects)
        {
            _projectCombo.Items.Add(project);
        }

        if (_projects.Count == 0)
        {
            _selectedPathLabel.Text = "Add a valid project before saving.";
            _saveButton.Enabled = false;
            return;
        }

        var selected = _projects.FindIndex(project => string.Equals(project.Path, suggestedProjectPath, StringComparison.OrdinalIgnoreCase));
        _projectCombo.SelectedIndex = selected >= 0 ? selected : 0;
        UpdatePathLabel();
    }

    private async Task UpdateAiStatusAsync()
    {
        var setupState = await _aiFolderNameService.GetSetupStateAsync();
        _aiSetupLink.Text = $"Install/Setup local AI: {setupState.SetupUrl}";
        _aiSetupLink.Links.Clear();
        _aiSetupLink.Links.Add(0, _aiSetupLink.Text.Length, setupState.SetupUrl);
        if (!_useLocalAiCheck.Checked)
        {
            _aiStatusLabel.ForeColor = System.Drawing.Color.DimGray;
            _aiStatusLabel.Text = "AI naming is disabled.";
            _aiSetupLink.Visible = false;
            return;
        }

        if (!setupState.IsOllamaInstalled)
        {
            _aiStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _aiStatusLabel.Text = "Ollama is not installed on this PC. Click the setup link to install it.";
            _aiSetupLink.Visible = true;
            return;
        }

        if (!setupState.IsServerAvailable)
        {
            _aiStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _aiStatusLabel.Text = "Ollama is installed but not running. Start Ollama to enable local AI naming.";
            _aiSetupLink.Visible = true;
            return;
        }

        if (!setupState.IsModelAvailable)
        {
            _aiStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            var detected = setupState.InstalledModelNames.Count > 0
                ? $"Detected models: {string.Join(", ", setupState.InstalledModelNames.Take(3))}."
                : "No models were detected.";
            _aiStatusLabel.Text = $"Ollama is running, but no compatible model is available. {detected}";
            _aiSetupLink.Visible = true;
            return;
        }

        _aiStatusLabel.ForeColor = System.Drawing.Color.DarkGreen;
        _aiStatusLabel.Text = $"AI naming ready using {setupState.SelectedModelName ?? _aiFolderNameService.ModelName}.";
        _aiSetupLink.Visible = false;
    }

    private async Task UpdateAiOptionAsync()
    {
        await _settingsService.SaveUseLocalAiFolderNamingAsync(_useLocalAiCheck.Checked);
        await UpdateAiStatusAsync();
    }

    private void UpdatePathLabel()
    {
        if (_projectCombo.SelectedItem is not ProjectOption option)
        {
            _selectedPathLabel.Text = "No valid project selected.";
            _saveButton.Enabled = false;
            return;
        }

        _selectedPathLabel.Text = option.Path;
        _saveButton.Enabled = Directory.Exists(option.Path);
    }

    private void ConfirmSelection()
    {
        if (_projectCombo.SelectedItem is not ProjectOption option)
        {
            return;
        }

        SelectedProjectPath = option.Path;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void PopulateRemoveList(IEnumerable<SavedProjectDefinition> savedProjects)
    {
        _removeListBox.Items.Clear();
        foreach (var proj in savedProjects
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            _removeListBox.Items.Add(new ProjectOption(proj.Name, proj.ProjectPath));
        }
    }

    private async Task RemoveProjectAsync()
    {
        _removeStatusLabel.Text = string.Empty;
        _removeStatusLabel.ForeColor = System.Drawing.Color.DarkGreen;

        if (_removeListBox.SelectedItem is not ProjectOption option)
        {
            _removeStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _removeStatusLabel.Text = "Select a project from the list first.";
            return;
        }

        try
        {
            await _settingsService.RemoveProjectAsync(option.Path);
            _projects.RemoveAll(p => string.Equals(p.Path, option.Path, StringComparison.OrdinalIgnoreCase));
            _removeListBox.Items.Remove(option);
            PopulateProjects(null);

            var settings = await _settingsService.LoadAsync();
            PopulateRemoveList(settings.SavedProjects);

            _removeStatusLabel.Text = $"\u2713 '{option.Name}' removed from the list.";
        }
        catch (Exception exception)
        {
            _removeStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _removeStatusLabel.Text = "Could not remove: " + exception.Message;
        }
    }

    private async Task AddProjectAsync()
    {
        _addStatusLabel.Text = string.Empty;
        _addStatusLabel.ForeColor = System.Drawing.Color.DarkGreen;

        if (string.IsNullOrWhiteSpace(_parentFolderTextBox.Text))
        {
            _addStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _addStatusLabel.Text = "Paste the project folder path.";
            return;
        }

        if (!Directory.Exists(_parentFolderTextBox.Text))
        {
            _addStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _addStatusLabel.Text = "That folder does not exist. Correct the path and try again.";
            return;
        }

        try
        {
            var saved = _editingProjectPath is null
                ? await _settingsService.AddProjectAsync(_projectNameTextBox.Text, _parentFolderTextBox.Text)
                : await _settingsService.EditProjectAsync(_editingProjectPath, _projectNameTextBox.Text, _parentFolderTextBox.Text);
            if (_editingProjectPath is not null)
            {
                _projects.RemoveAll(project => string.Equals(project.Path, _editingProjectPath, StringComparison.OrdinalIgnoreCase));
            }
            _projects.RemoveAll(project => string.Equals(project.Path, saved.ProjectPath, StringComparison.OrdinalIgnoreCase));
            _projects.Add(new ProjectOption(saved.Name, saved.ProjectPath));
            _projects.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
            PopulateProjects(saved.ProjectPath);
            var updatedSettings = await _settingsService.LoadAsync();
            PopulateRemoveList(updatedSettings.SavedProjects);
            _addStatusLabel.Text = _editingProjectPath is null
                ? $"\u2713 '{saved.Name}' added and selected."
                : $"\u2713 '{saved.Name}' updated and selected.";
            _editingProjectPath = null;
            _addButton.Text = "Add Project";
            _parentFolderTextBox.Clear();
            _projectNameTextBox.Clear();
            _tabs.SelectedIndex = 0;
        }
        catch (Exception exception)
        {
            _addStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _addStatusLabel.Text = "Could not add project: " + exception.Message;
        }
    }

    private void BeginEditProject()
    {
        _removeStatusLabel.Text = string.Empty;
        if (_removeListBox.SelectedItem is not ProjectOption option)
        {
            _removeStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _removeStatusLabel.Text = "Select a project to edit.";
            return;
        }

        _editingProjectPath = option.Path;
        _parentFolderTextBox.Text = option.Path;
        _projectNameTextBox.Text = option.Name;
        _addButton.Text = "Save Changes";
        _tabs.SelectedIndex = 1;
        _projectNameTextBox.Focus();
        _projectNameTextBox.SelectAll();
    }

    private void UpdateAddButtonState()
    {
        _addButton.Enabled = !string.IsNullOrWhiteSpace(_parentFolderTextBox.Text)
            && Directory.Exists(_parentFolderTextBox.Text.Trim());
    }

    private void UpdateLocationAndNicknameState()
    {
        var path = _parentFolderTextBox.Text.Trim();
        var isValid = Directory.Exists(path);
        _projectNameTextBox.Enabled = isValid;
        if (isValid)
        {
            _projectNameTextBox.Text = SuggestNickname(path);
        }
        else
        {
            _projectNameTextBox.Clear();
        }

        UpdateAddButtonState();
    }

    private static string SuggestNickname(string path)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        if ((directory.Name.Equals("EMAIL", StringComparison.OrdinalIgnoreCase)
                || directory.Name.Equals("EMAILS", StringComparison.OrdinalIgnoreCase))
            && directory.Parent is not null)
        {
            return directory.Parent.Name;
        }

        return directory.Name;
    }

    private sealed record ProjectOption(string Name, string Path)
    {
        public override string ToString() => Name;
    }
}
