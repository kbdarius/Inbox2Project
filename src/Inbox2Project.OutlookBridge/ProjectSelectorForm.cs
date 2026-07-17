using Inbox2Project.Models;
using Inbox2Project.Services;
using Button = System.Windows.Forms.Button;
using ComboBox = System.Windows.Forms.ComboBox;
using DialogResult = System.Windows.Forms.DialogResult;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using Form = System.Windows.Forms.Form;
using Label = System.Windows.Forms.Label;
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

    public ProjectSelectorForm(
        ISettingsService settingsService,
        IReadOnlyList<string> projectPaths,
        SettingsModel settings,
        string? suggestedProjectPath)
    {
        _settingsService = settingsService;
        _projects = BuildProjectOptions(projectPaths, settings.SavedProjects);

        Text = "Inbox2Project - Select Project";
        Width = 620;
        Height = 340;
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

        var useSelectedButton = new Button
        {
            Left = 380,
            Top = 180,
            Width = 180,
            Height = 32,
            Text = "Use Selected Project",
        };
        useSelectedButton.Click += (_, _) => ConfirmSelection();

        var useDefaultButton = new Button
        {
            Left = 20,
            Top = 180,
            Width = 180,
            Height = 32,
            Text = "Use Default",
        };
        useDefaultButton.Click += (_, _) => UseDefaultProject();

        selectTab.Controls.Add(new Label { Left = 20, Top = 12, Width = 200, Text = "Project" });
        selectTab.Controls.Add(_projectCombo);
        selectTab.Controls.Add(new Label { Left = 20, Top = 132, Width = 320, Text = "If you close this window or select nothing, the default project is used." });
        selectTab.Controls.Add(_selectedPathLabel);
        selectTab.Controls.Add(useSelectedButton);
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
            Width = 430,
        };

        var browseButton = new Button
        {
            Left = 460,
            Top = 98,
            Width = 100,
            Height = 28,
            Text = "Browse...",
        };
        browseButton.Click += (_, _) => BrowseForFolder();

        var addButton = new Button
        {
            Left = 380,
            Top = 180,
            Width = 180,
            Height = 32,
            Text = "Add Project",
        };
        addButton.Click += (_, _) => AddProject();

        addTab.Controls.Add(new Label { Left = 20, Top = 12, Width = 200, Text = "Project Name" });
        addTab.Controls.Add(_projectNameTextBox);
        addTab.Controls.Add(new Label { Left = 20, Top = 80, Width = 260, Text = "Parent Folder To Save This Project Under" });
        addTab.Controls.Add(_parentFolderTextBox);
        addTab.Controls.Add(browseButton);
        addTab.Controls.Add(addButton);

        _tabs.Controls.Add(selectTab);
        _tabs.Controls.Add(addTab);
        Controls.Add(_tabs);

        _parentFolderTextBox.Text = settings.ProjectsRoot;

        PopulateProjects(suggestedProjectPath);
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

    private void BrowseForFolder()
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "Choose the parent folder where this project should be created.";
        if (Directory.Exists(_parentFolderTextBox.Text))
        {
            dialog.SelectedPath = _parentFolderTextBox.Text;
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _parentFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void AddProject()
    {
        if (string.IsNullOrWhiteSpace(_projectNameTextBox.Text))
        {
            MessageBox.Show("Enter a project name.", "Inbox2Project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_parentFolderTextBox.Text))
        {
            MessageBox.Show("Choose where the project should be saved.", "Inbox2Project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var saved = _settingsService.AddProjectAsync(_projectNameTextBox.Text, _parentFolderTextBox.Text).GetAwaiter().GetResult();
            _projects.RemoveAll(project => string.Equals(project.Path, saved.ProjectPath, StringComparison.OrdinalIgnoreCase));
            _projects.Add(new ProjectOption(saved.Name, saved.ProjectPath));
            _projects.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
            PopulateProjects(saved.ProjectPath);
            SelectedProjectPath = saved.ProjectPath;
            _tabs.SelectedIndex = 0;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show("Could not add project.\n\n" + exception.Message, "Inbox2Project", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed record ProjectOption(string Name, string Path)
    {
        public override string ToString() => Name;
    }
}
