using Inbox2Project.Models;
using Inbox2Project.Services;
using Button = System.Windows.Forms.Button;
using ComboBox = System.Windows.Forms.ComboBox;
using DialogResult = System.Windows.Forms.DialogResult;
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

    private readonly ComboBox _projectCombo;
    private readonly Label _selectedPathLabel;
    private readonly TextBox _projectNameTextBox;
    private readonly TextBox _parentFolderTextBox;
    private readonly Label _addStatusLabel;
    private readonly ListBox _removeListBox;
    private readonly Label _removeStatusLabel;

    public ProjectSelectorForm(
        ISettingsService settingsService,
        IReadOnlyList<string> projectPaths,
        SettingsModel settings,
        string? suggestedProjectPath)
    {
        _settingsService = settingsService;
        _projects = BuildProjectOptions(projectPaths, settings.SavedProjects);

        Text = "Inbox2Project - Select Project";
        Width = 640;
        Height = 380;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;

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

        _selectedPathLabel = new Label
        {
            Left = 20,
            Top = 76,
            Width = 540,
            Height = 48,
        };

        var saveButton = new Button
        {
            Left = 380,
            Top = 200,
            Width = 180,
            Height = 36,
            Text = "Save to Selected Project",
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
        };
        saveButton.Click += (_, _) => ConfirmSelection();

        var useDefaultButton = new Button
        {
            Left = 20,
            Top = 200,
            Width = 160,
            Height = 36,
            Text = "Save to Default",
        };
        useDefaultButton.Click += (_, _) => UseDefaultProject();

        selectTab.Controls.Add(new Label { Left = 20, Top = 12, Width = 200, Text = "Select project to save into:" });
        selectTab.Controls.Add(_projectCombo);
        selectTab.Controls.Add(_selectedPathLabel);
        selectTab.Controls.Add(new Label { Left = 20, Top = 162, Width = 540, Height = 32, Text = "Tip: add a new project on the Add Project tab first, then return here to select it." });
        selectTab.Controls.Add(saveButton);
        selectTab.Controls.Add(useDefaultButton);

        _projectNameTextBox = new TextBox
        {
            Left = 20,
            Top = 32,
            Width = 540,
        };

        _parentFolderTextBox = new TextBox
        {
            Left = 20,
            Top = 100,
            Width = 540,
        };

        var addButton = new Button
        {
            Left = 380,
            Top = 200,
            Width = 180,
            Height = 36,
            Text = "Add Project",
        };
        addButton.Click += async (_, _) => await AddProjectAsync();

        _addStatusLabel = new Label
        {
            Left = 20,
            Top = 244,
            Width = 540,
            Height = 32,
            ForeColor = System.Drawing.Color.DarkGreen,
        };

        addTab.Controls.Add(new Label { Left = 20, Top = 12, Width = 200, Text = "Project Name" });
        addTab.Controls.Add(_projectNameTextBox);
        addTab.Controls.Add(new Label { Left = 20, Top = 80, Width = 260, Text = "Parent Folder (must already exist)" });
        addTab.Controls.Add(_parentFolderTextBox);
        addTab.Controls.Add(new Label { Left = 20, Top = 132, Width = 540, Text = "Paste an existing folder path. The project folder will be created inside it." });
        addTab.Controls.Add(addButton);
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

        _removeStatusLabel = new Label
        {
            Left = 20,
            Top = 230,
            Width = 540,
            Height = 32,
            ForeColor = System.Drawing.Color.DarkGreen,
        };

        manageTab.Controls.Add(new Label { Left = 20, Top = 12, Width = 540, Text = "Custom saved projects (Default cannot be removed):" });
        manageTab.Controls.Add(_removeListBox);
        manageTab.Controls.Add(new Label { Left = 20, Top = 184, Width = 320, Height = 36, Text = "Removing a project only removes it from\nthis list. No files are deleted." });
        manageTab.Controls.Add(removeButton);
        manageTab.Controls.Add(_removeStatusLabel);

        _tabs.Controls.Add(selectTab);
        _tabs.Controls.Add(addTab);
        _tabs.Controls.Add(manageTab);
        Controls.Add(_tabs);

        _parentFolderTextBox.Text = settings.ProjectsRoot;

        PopulateProjects(suggestedProjectPath);
        PopulateRemoveList(settings.SavedProjects);
    }

    public string? SelectedProjectPath { get; private set; }

    private static List<ProjectOption> BuildProjectOptions(IReadOnlyList<string> projectPaths, IReadOnlyList<SavedProjectDefinition> savedProjects)
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
            .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
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
            return;
        }

        var selected = _projects.FindIndex(project => string.Equals(project.Path, suggestedProjectPath, StringComparison.OrdinalIgnoreCase));
        _projectCombo.SelectedIndex = selected >= 0 ? selected : 0;
        UpdatePathLabel();
    }

    private void UpdatePathLabel()
    {
        if (_projectCombo.SelectedItem is not ProjectOption option)
        {
            _selectedPathLabel.Text = "No project selected. Default will be used.";
            return;
        }

        _selectedPathLabel.Text = option.Path;
    }

    private void ConfirmSelection()
    {
        if (_projectCombo.SelectedItem is not ProjectOption option)
        {
            UseDefaultProject();
            return;
        }

        SelectedProjectPath = option.Path;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UseDefaultProject()
    {
        var defaultProject = _projects.FirstOrDefault(project => string.Equals(project.Name, "Default", StringComparison.OrdinalIgnoreCase)) ?? _projects.FirstOrDefault();
        SelectedProjectPath = defaultProject?.Path;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void PopulateRemoveList(IEnumerable<SavedProjectDefinition> savedProjects)
    {
        _removeListBox.Items.Clear();
        foreach (var proj in savedProjects
            .Where(p => !string.Equals(p.Name, "Default", StringComparison.OrdinalIgnoreCase))
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

        if (string.IsNullOrWhiteSpace(_projectNameTextBox.Text))
        {
            _addStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _addStatusLabel.Text = "Enter a project name.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_parentFolderTextBox.Text))
        {
            _addStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _addStatusLabel.Text = "Paste a parent folder path.";
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
            var saved = await _settingsService.AddProjectAsync(_projectNameTextBox.Text, _parentFolderTextBox.Text);
            _projects.RemoveAll(project => string.Equals(project.Path, saved.ProjectPath, StringComparison.OrdinalIgnoreCase));
            _projects.Add(new ProjectOption(saved.Name, saved.ProjectPath));
            _projects.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
            PopulateProjects(saved.ProjectPath);
            var updatedSettings = await _settingsService.LoadAsync();
            PopulateRemoveList(updatedSettings.SavedProjects);
            _addStatusLabel.Text = $"\u2713 '{saved.Name}' added. Go to Select Project tab to use it.";
            _projectNameTextBox.Clear();
        }
        catch (Exception exception)
        {
            _addStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _addStatusLabel.Text = "Could not add project: " + exception.Message;
        }
    }

    private sealed record ProjectOption(string Name, string Path)
    {
        public override string ToString() => Name;
    }
}
