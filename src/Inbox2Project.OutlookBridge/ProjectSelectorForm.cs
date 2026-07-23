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
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using TabControl = System.Windows.Forms.TabControl;
using TabPage = System.Windows.Forms.TabPage;
using TextBox = System.Windows.Forms.TextBox;

namespace Inbox2Project.OutlookBridge;

internal sealed class ProjectSelectorForm : Form
{
    private readonly ISettingsService _settingsService;
    private readonly IPathSafetyService _pathSafetyService;
    private readonly List<ProjectOption> _projects;
    private readonly TabControl _tabs;
    private IAiFolderNameService _aiFolderNameService;
    private readonly OpenAiFolderNameService _openAiService;
    private readonly GitHubModelsFolderNameService _gitHubModelsService;

    private readonly ComboBox _projectCombo;
    private readonly Label _selectedPathLabel;
    private readonly Button _saveButton;
    private readonly TextBox _finalNameTextBox;
    private readonly CheckBox _includeSenderCheck;
    private readonly string _baseSuggestedName;
    private readonly string _senderName;
    private readonly DateTimeOffset _receivedAt;
    private readonly ComboBox _aiProviderCombo;
    private readonly Label _aiStatusLabel;
    private readonly LinkLabel _aiSetupLink;
    private readonly CheckBox _saveAsMsgCheck;
    private readonly Label _finalNamePreviewLabel;
    private readonly TextBox _projectNameTextBox;
    private readonly TextBox _parentFolderTextBox;
    private readonly Button _addButton;
    private readonly Label _addStatusLabel;
    private readonly ListBox _removeListBox;
    private readonly Label _removeStatusLabel;
    private readonly System.Windows.Forms.ToolTip _toolTip;
    private string? _editingProjectPath;
    private bool _suppressFinalNameUpdate;

    public ProjectSelectorForm(
        ISettingsService settingsService,
        IPathSafetyService pathSafetyService,
        IReadOnlyList<string> projectPaths,
        SettingsModel settings,
        string? suggestedProjectPath,
        string suggestedBaseName,
        string senderName,
        DateTimeOffset receivedAt,
        OpenAiFolderNameService openAiService,
        GitHubModelsFolderNameService gitHubModelsService)
    {
        _settingsService = settingsService;
        _pathSafetyService = pathSafetyService;
        _openAiService = openAiService;
        _gitHubModelsService = gitHubModelsService;
        _aiFolderNameService = settings.AiProvider == AiNamingProvider.GitHubModels
            ? (IAiFolderNameService)gitHubModelsService
            : openAiService;
        _baseSuggestedName = suggestedBaseName ?? string.Empty;
        _senderName = senderName ?? string.Empty;
        _receivedAt = receivedAt;
        _projects = BuildProjectOptions(projectPaths, settings.SavedProjects, settings.LastSelectedProject);

        Text = AppInfo.WindowTitle("Select Project");
        Width = 860;
        Height = 720;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new System.Drawing.Size(860, 400);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = true;
        MinimizeBox = true;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new System.Drawing.Font("Segoe UI", 9F);
        BackColor = System.Drawing.Color.FromArgb(242, 246, 250);

        _toolTip = new System.Windows.Forms.ToolTip
        {
            AutoPopDelay = 10000,
            InitialDelay = 400,
            ReshowDelay = 100,
            ShowAlways = true,
        };

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new System.Drawing.Point(14, 6),
        };

        var selectTab = new TabPage("Select Project") { BackColor = System.Drawing.Color.FromArgb(238, 243, 248) };
        var addTab = new TabPage("Add Project") { BackColor = System.Drawing.Color.FromArgb(238, 243, 248) };

        _projectCombo = new ComboBox
        {
            Left = 24,
            Top = 100,
            Width = 780,
            Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DropDownWidth = 780,
            DropDownHeight = 360,
            IntegralHeight = false,
            MaxDropDownItems = 12,
        };
        _projectCombo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _projectCombo.SelectedIndexChanged += (_, _) => UpdatePathLabel();
        _projectCombo.DoubleClick += (_, _) => ConfirmSelection();

        _selectedPathLabel = new Label
        {
            Left = 24,
            Top = 454,
            Width = 780,
            Height = 48,
            AutoSize = false,
            AutoEllipsis = true,
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
            BackColor = System.Drawing.Color.White,
            Padding = new System.Windows.Forms.Padding(8, 4, 8, 4),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        _saveButton = new Button
        {
            Left = 584,
            Top = 550,
            Width = 220,
            Height = 40,
            Text = "Save to Selected Project",
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(0, 112, 120),
            ForeColor = System.Drawing.Color.White,
            UseVisualStyleBackColor = false,
        };
        _saveButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _saveButton.Click += (_, _) => ConfirmSelection();

        _finalNameTextBox = new TextBox
        {
            Left = 24,
            Top = 170,
            Width = 780,
            Height = 30,
            MaxLength = 255,
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
            BackColor = System.Drawing.Color.White,
            Text = _baseSuggestedName,
        };
        _finalNameTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _finalNameTextBox.TextChanged += (_, _) => NormalizeAndPreviewFinalName();

        _finalNamePreviewLabel = new Label
        {
            Left = 24,
            Top = 208,
            Width = 780,
            Height = 54,
            ForeColor = System.Drawing.Color.DimGray,
            Text = string.Empty,
            AutoSize = false,
            AutoEllipsis = false,
            UseCompatibleTextRendering = true,
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
            BackColor = System.Drawing.Color.White,
            Padding = new System.Windows.Forms.Padding(8, 5, 8, 5),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        _includeSenderCheck = new CheckBox
        {
            Left = 24,
            Top = 278,
            Width = 780,
            Text = "Include sender name in file name (sender_subject)",
            Enabled = !string.IsNullOrWhiteSpace(_senderName),
            Checked = !string.IsNullOrWhiteSpace(_senderName),
        };
        _includeSenderCheck.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _includeSenderCheck.CheckedChanged += (_, _) => ApplySenderNameToggle();

        _aiProviderCombo = new ComboBox
        {
            Left = 24,
            Top = 310,
            Width = 780,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _aiProviderCombo.Items.Add("No AI naming");
        _aiProviderCombo.Items.Add("OpenAI API (gpt-5-nano)");
        _aiProviderCombo.Items.Add("GitHub Models via GitHub PAT (gpt-4o-mini)");
        _aiProviderCombo.SelectedIndex = settings.AiProvider switch
        {
            AiNamingProvider.OpenAi => 1,
            AiNamingProvider.GitHubModels => 2,
            _ => 0,
        };
        _aiProviderCombo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _aiProviderCombo.SelectedIndexChanged += async (_, _) => await UpdateAiOptionAsync();

        _aiStatusLabel = new Label
        {
            Left = 24,
            Top = 342,
            Width = 780,
            Height = 42,
            Text = "Checking AI setup...",
            AutoSize = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        _aiSetupLink = new LinkLabel
        {
            Left = 24,
            Top = 388,
            Width = 780,
            Height = 28,
            Text = "Set up or update OpenAI API key",
            Visible = false,
        };
        _aiSetupLink.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _aiSetupLink.Links.Add(0, _aiSetupLink.Text.Length);
        _aiSetupLink.LinkClicked += async (_, _) =>
        {
            if (_aiFolderNameService is GitHubModelsFolderNameService ghService)
            {
                using var form = new GitHubModelsApiKeySetupForm(ghService);
                form.ShowDialog(this);
                await UpdateAiStatusAsync();
                return;
            }

            if (_aiFolderNameService is OpenAiFolderNameService openAiSvc)
            {
                using var form = new OpenAiApiKeySetupForm(openAiSvc);
                form.ShowDialog(this);
                await UpdateAiStatusAsync();
                return;
            }

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(_aiFolderNameService.DownloadUrl) { UseShellExecute = true });
        };

        _saveAsMsgCheck = new CheckBox
        {
            Left = 24,
            Top = 420,
            Width = 780,
            Height = 24,
            Text = "Save email as Outlook message (.msg) to preserve formatting, images, and tables",
        };
        _saveAsMsgCheck.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _saveAsMsgCheck.CheckedChanged += (_, _) => UpdateFinalNamePreview();

        selectTab.Controls.Add(new Label
        {
            Left = 24,
            Top = 12,
            Width = 780,
            Height = 28,
            Text = "Save this email to a project",
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
            BackColor = System.Drawing.Color.FromArgb(32, 99, 155),
            ForeColor = System.Drawing.Color.White,
            Padding = new System.Windows.Forms.Padding(10, 4, 0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        });
        selectTab.Controls.Add(new Label
        {
            Left = 24,
            Top = 46,
            Width = 780,
            Height = 22,
            Text = "Choose a destination, review the file name, then save.",
            ForeColor = System.Drawing.Color.DimGray,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        });
        selectTab.Controls.Add(new Label { Left = 24, Top = 78, Width = 400, Text = "Project destination:" });
        selectTab.Controls.Add(_projectCombo);
        selectTab.Controls.Add(new Label { Left = 24, Top = 148, Width = 500, Text = "File name (before date prefix and extension):" });
        selectTab.Controls.Add(_finalNameTextBox);
        selectTab.Controls.Add(_finalNamePreviewLabel);
        selectTab.Controls.Add(_includeSenderCheck);
        selectTab.Controls.Add(_aiProviderCombo);
        selectTab.Controls.Add(_aiStatusLabel);
        selectTab.Controls.Add(_aiSetupLink);
        selectTab.Controls.Add(_saveAsMsgCheck);
        selectTab.Controls.Add(_selectedPathLabel);
        var selectTipLabel = new Label
        {
            Left = 24,
            Top = 514,
            Width = 780,
            Height = 28,
            Text = "Tip: add a new project on the Add Project tab first, then return here to select it.",
            AutoSize = false,
            ForeColor = System.Drawing.Color.DimGray,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        selectTab.Controls.Add(selectTipLabel);
        selectTab.Controls.Add(_saveButton);

        _parentFolderTextBox = new TextBox
        {
            Left = 24,
            Top = 94,
            Width = 780,
            Height = 30,
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
            BackColor = System.Drawing.Color.White,
        };
        _parentFolderTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _parentFolderTextBox.TextChanged += (_, _) => UpdateLocationAndNicknameState();

        _projectNameTextBox = new TextBox
        {
            Left = 24,
            Top = 158,
            Width = 780,
            Height = 30,
            MaxLength = 255,
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
            BackColor = System.Drawing.Color.White,
            Enabled = false,
        };
        _projectNameTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _projectNameTextBox.TextChanged += (_, _) => UpdateAddButtonState();

        _addButton = new Button
        {
            Left = 624,
            Top = 246,
            Width = 180,
            Height = 40,
            Text = "Add Project",
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(0, 112, 120),
            ForeColor = System.Drawing.Color.White,
            UseVisualStyleBackColor = false,
        };
        _addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _addButton.Click += async (_, _) => await AddProjectAsync();

        _addStatusLabel = new Label
        {
            Left = 24,
            Top = 294,
            Width = 780,
            Height = 32,
            ForeColor = System.Drawing.Color.DarkGreen,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        addTab.Controls.Add(new Label
        {
            Left = 24,
            Top = 12,
            Width = 780,
            Height = 28,
            Text = "Add or edit a project",
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
            BackColor = System.Drawing.Color.FromArgb(32, 99, 155),
            ForeColor = System.Drawing.Color.White,
            Padding = new System.Windows.Forms.Padding(10, 4, 0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        });
        addTab.Controls.Add(new Label { Left = 24, Top = 72, Width = 500, Text = "Destination folder (must already exist):" });
        addTab.Controls.Add(_parentFolderTextBox);
        addTab.Controls.Add(new Label { Left = 24, Top = 136, Width = 260, Text = "Project nickname (optional):" });
        addTab.Controls.Add(_projectNameTextBox);
        addTab.Controls.Add(new Label
        {
            Left = 24,
            Top = 198,
            Width = 780,
            Height = 40,
            Text = "Files are saved directly to this location. The nickname is suggested from the folder name.",
            AutoSize = false,
            ForeColor = System.Drawing.Color.DimGray,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        });
        addTab.Controls.Add(_addButton);
        addTab.Controls.Add(_addStatusLabel);

        // ── Manage tab ──────────────────────────────────────────────
        var manageTab = new TabPage("Manage Projects") { BackColor = System.Drawing.Color.FromArgb(238, 243, 248) };

        _removeListBox = new ListBox
        {
            Left = 24,
            Top = 52,
            Width = 780,
            Height = 190,
            SelectionMode = SelectionMode.One,
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
            BackColor = System.Drawing.Color.White,
        };
        _removeListBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var removeButton = new Button
        {
            Left = 624,
            Top = 270,
            Width = 180,
            Height = 40,
            Text = "Remove Selected",
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(0, 112, 120),
            ForeColor = System.Drawing.Color.White,
            UseVisualStyleBackColor = false,
        };
        removeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        removeButton.Click += async (_, _) => await RemoveProjectAsync();

        var editButton = new Button
        {
            Left = 468,
            Top = 270,
            Width = 140,
            Height = 40,
            Text = "Edit Selected",
            FlatStyle = FlatStyle.Flat,
            BackColor = System.Drawing.Color.FromArgb(232, 239, 246),
            ForeColor = System.Drawing.Color.FromArgb(35, 58, 80),
        };
        editButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        editButton.Click += (_, _) => BeginEditProject();

        _removeStatusLabel = new Label
        {
            Left = 24,
            Top = 320,
            Width = 780,
            Height = 32,
            ForeColor = System.Drawing.Color.DarkGreen,
            AutoSize = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        manageTab.Controls.Add(new Label
        {
            Left = 24,
            Top = 12,
            Width = 780,
            Height = 28,
            Text = "Manage saved projects",
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
            BackColor = System.Drawing.Color.FromArgb(32, 99, 155),
            ForeColor = System.Drawing.Color.White,
            Padding = new System.Windows.Forms.Padding(10, 4, 0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        });
        manageTab.Controls.Add(_removeListBox);
        var manageHintLabel = new Label { Left = 24, Top = 244, Width = 300, Height = 24, Text = "Removing a project keeps all files on disk.", ForeColor = System.Drawing.Color.DimGray };
        manageTab.Controls.Add(manageHintLabel);
        manageTab.Controls.Add(removeButton);
        manageTab.Controls.Add(editButton);
        manageTab.Controls.Add(_removeStatusLabel);

        var selectActionsPanel = new System.Windows.Forms.Panel
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            BackColor = System.Drawing.Color.FromArgb(236, 244, 249),
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
        };
        selectTab.Controls.Add(selectActionsPanel);
        selectActionsPanel.Controls.Add(selectTipLabel);
        selectActionsPanel.Controls.Add(_saveButton);
        selectTipLabel.Left = 16;
        selectTipLabel.Top = 22;
        _saveButton.Top = 14;

        var addActionsPanel = new System.Windows.Forms.Panel
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            BackColor = System.Drawing.Color.FromArgb(236, 244, 249),
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
        };
        addTab.Controls.Add(addActionsPanel);
        addActionsPanel.Controls.Add(_addStatusLabel);
        addActionsPanel.Controls.Add(_addButton);
        _addStatusLabel.Left = 16;
        _addStatusLabel.Top = 20;
        _addButton.Top = 14;

        var manageActionsPanel = new System.Windows.Forms.Panel
        {
            Dock = DockStyle.Bottom,
            Height = 96,
            BackColor = System.Drawing.Color.FromArgb(236, 244, 249),
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
        };
        manageTab.Controls.Add(manageActionsPanel);
        manageActionsPanel.Controls.Add(manageHintLabel);
        manageActionsPanel.Controls.Add(editButton);
        manageActionsPanel.Controls.Add(removeButton);
        manageActionsPanel.Controls.Add(_removeStatusLabel);
        manageHintLabel.Left = 16;
        manageHintLabel.Top = 18;
        editButton.Top = 12;
        removeButton.Top = 12;
        _removeStatusLabel.Left = 16;
        _removeStatusLabel.Top = 58;

        void LayoutActionPanels()
        {
            _saveButton.Left = Math.Max(16, selectActionsPanel.ClientSize.Width - 16 - _saveButton.Width);
            selectTipLabel.Width = Math.Max(160, _saveButton.Left - 32);

            _addButton.Left = Math.Max(16, addActionsPanel.ClientSize.Width - 16 - _addButton.Width);
            _addStatusLabel.Width = Math.Max(160, _addButton.Left - 32);

            removeButton.Left = Math.Max(16, manageActionsPanel.ClientSize.Width - 16 - removeButton.Width);
            editButton.Left = Math.Max(16, removeButton.Left - 12 - editButton.Width);
            manageHintLabel.Width = Math.Max(160, editButton.Left - 32);
            _removeStatusLabel.Width = Math.Max(160, manageActionsPanel.ClientSize.Width - 32);
        }

        selectActionsPanel.Resize += (_, _) => LayoutActionPanels();
        addActionsPanel.Resize += (_, _) => LayoutActionPanels();
        manageActionsPanel.Resize += (_, _) => LayoutActionPanels();

        _tabs.Controls.Add(selectTab);
        _tabs.Controls.Add(addTab);
        _tabs.Controls.Add(manageTab);
        Controls.Add(_tabs);

        PopulateProjects(suggestedProjectPath);
        PopulateRemoveList(settings.SavedProjects);
        UpdateAddButtonState();
        ApplySenderNameToggle();
        NormalizeAndPreviewFinalName();
        selectTab.Resize += (_, _) => FitWideControls(selectTab);
        addTab.Resize += (_, _) => FitWideControls(addTab);
        manageTab.Resize += (_, _) => FitWideControls(manageTab);
        _tabs.SelectedIndexChanged += (_, _) => ResizeForSelectedTab();
        Load += async (_, _) =>
        {
            ResizeForSelectedTab();
            FitWideControls(selectTab);
            FitWideControls(addTab);
            FitWideControls(manageTab);
            LayoutActionPanels();
            await UpdateAiStatusAsync();
        };

        _toolTip.SetToolTip(_projectCombo, "Select the project folder where this email will be saved.");
        _toolTip.SetToolTip(_finalNameTextBox, "Use letters, numbers, spaces, underscores, periods, and dashes. Maximum 255 characters.");
        _toolTip.SetToolTip(_finalNamePreviewLabel, "The complete file name that will be created.");
        _toolTip.SetToolTip(_parentFolderTextBox, "Enter an existing folder path.");
        _toolTip.SetToolTip(_projectNameTextBox, "Optional display name used in the project list.");
    }

    public string? SelectedProjectPath { get; private set; }

    public string? SelectedFinalName { get; private set; }

    public bool SelectedSaveAsMsg { get; private set; }

    private void ResizeForSelectedTab()
    {
        var desiredClientHeight = _tabs.SelectedIndex switch
        {
            1 => 370,
            2 => 400,
            _ => 620,
        };

        var windowChromeHeight = Height - ClientSize.Height;
        var desiredWindowHeight = desiredClientHeight + windowChromeHeight;
        MinimumSize = new System.Drawing.Size(860, desiredWindowHeight);
        Height = desiredWindowHeight;
    }

    private static void FitWideControls(TabPage tab)
    {
        var contentWidth = Math.Max(320, tab.ClientSize.Width - 48);
        foreach (System.Windows.Forms.Control control in tab.Controls)
        {
            if (control.Dock != DockStyle.None || control is Button || control.Width < 600)
            {
                continue;
            }

            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            control.Width = contentWidth;

            if (control is TextBox textBox)
            {
                textBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                textBox.BackColor = System.Drawing.Color.White;
            }
            else if (control is ListBox listBox)
            {
                listBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                listBox.BackColor = System.Drawing.Color.White;
            }
        }
    }

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
        var selectedProvider = _aiProviderCombo.SelectedIndex switch
        {
            1 => AiNamingProvider.OpenAi,
            2 => AiNamingProvider.GitHubModels,
            _ => AiNamingProvider.None,
        };

        _aiFolderNameService = selectedProvider == AiNamingProvider.GitHubModels
            ? (IAiFolderNameService)_gitHubModelsService
            : _openAiService;

        if (selectedProvider == AiNamingProvider.None)
        {
            _aiStatusLabel.ForeColor = System.Drawing.Color.DimGray;
            _aiStatusLabel.Text = "AI naming is off.";
            _aiSetupLink.Visible = false;
            return;
        }

        bool isGitHub = selectedProvider == AiNamingProvider.GitHubModels;
        _aiSetupLink.Text = isGitHub ? "Set up or update GitHub PAT" : "Set up or update OpenAI API key";
        _aiSetupLink.Links.Clear();
        _aiSetupLink.Links.Add(0, _aiSetupLink.Text.Length);

        var setupState = await _aiFolderNameService.GetSetupStateAsync();

        if (!setupState.IsOllamaInstalled)
        {
            _aiStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _aiStatusLabel.Text = isGitHub
                ? "Add a GitHub PAT to enable AI naming via GitHub Models."
                : "Add an OpenAI API key to enable AI naming.";
            _aiSetupLink.Visible = true;
            return;
        }

        if (!setupState.IsServerAvailable)
        {
            _aiStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _aiStatusLabel.Text = isGitHub
                ? "GitHub Models could not be reached. Check the internet connection and try again."
                : "The OpenAI service could not be reached. Check the internet connection and try again.";
            _aiSetupLink.Visible = true;
            return;
        }

        if (!setupState.IsModelAvailable)
        {
            _aiStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
            _aiStatusLabel.Text = isGitHub
                ? "The GitHub PAT was not accepted. Verify the token and try again."
                : "The API key was not accepted or does not have access to gpt-5-nano.";
            _aiSetupLink.Visible = true;
            return;
        }

        _aiStatusLabel.ForeColor = System.Drawing.Color.DarkGreen;
        _aiStatusLabel.Text = isGitHub
            ? $"GitHub Models naming is ready ({setupState.SelectedModelName ?? _aiFolderNameService.ModelName})."
            : $"OpenAI naming is ready ({setupState.SelectedModelName ?? _aiFolderNameService.ModelName}).";
        _aiSetupLink.Visible = true;
    }

    private async Task UpdateAiOptionAsync()
    {
        var provider = _aiProviderCombo.SelectedIndex switch
        {
            1 => AiNamingProvider.OpenAi,
            2 => AiNamingProvider.GitHubModels,
            _ => AiNamingProvider.None,
        };
        await _settingsService.SaveAiProviderAsync(provider);
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
        _toolTip.SetToolTip(_selectedPathLabel, option.Path);
        _saveButton.Enabled = Directory.Exists(option.Path);
    }

    private void ApplySenderNameToggle()
    {
        if (string.IsNullOrWhiteSpace(_senderName))
        {
            UpdateFinalNamePreview();
            return;
        }

        var senderToken = _pathSafetyService.SanitizeName(_senderName, "sender");
        var current = _pathSafetyService.SanitizeName(_finalNameTextBox.Text, _baseSuggestedName);
        var subjectPart = current;
        var newPrefix = senderToken + "_";
        var legacySuffix = "_-_" + senderToken;

        if (subjectPart.StartsWith(newPrefix, StringComparison.OrdinalIgnoreCase))
        {
            subjectPart = subjectPart[newPrefix.Length..];
        }
        else if (subjectPart.EndsWith(legacySuffix, StringComparison.OrdinalIgnoreCase))
        {
            subjectPart = subjectPart[..^legacySuffix.Length];
        }

        if (_includeSenderCheck.Checked)
        {
            _finalNameTextBox.Text = string.IsNullOrWhiteSpace(subjectPart)
                ? senderToken
                : senderToken + "_" + subjectPart;
        }
        else
        {
            _finalNameTextBox.Text = subjectPart;
        }

        NormalizeAndPreviewFinalName();
    }

    private void ConfirmSelection()
    {
        if (_projectCombo.SelectedItem is not ProjectOption option)
        {
            return;
        }

        var finalName = _pathSafetyService.SanitizeName(_finalNameTextBox.Text, _baseSuggestedName);
        SelectedFinalName = string.IsNullOrWhiteSpace(finalName) ? _baseSuggestedName : finalName;
        SelectedProjectPath = option.Path;
        SelectedSaveAsMsg = _saveAsMsgCheck.Checked;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void NormalizeAndPreviewFinalName()
    {
        if (_suppressFinalNameUpdate)
        {
            return;
        }

        _suppressFinalNameUpdate = true;
        try
        {
            var original = _finalNameTextBox.Text;
            var normalized = _pathSafetyService.SanitizeName(original, _baseSuggestedName);
            if (!string.Equals(original, normalized, StringComparison.Ordinal))
            {
                var caret = _finalNameTextBox.SelectionStart;
                _finalNameTextBox.Text = normalized;
                _finalNameTextBox.SelectionStart = Math.Min(caret, _finalNameTextBox.Text.Length);
            }
        }
        finally
        {
            _suppressFinalNameUpdate = false;
        }

        UpdateFinalNamePreview();
    }

    private void UpdateFinalNamePreview()
    {
        var normalized = _pathSafetyService.SanitizeName(_finalNameTextBox.Text, _baseSuggestedName);
        var extension = _saveAsMsgCheck.Checked ? ".msg" : ".txt";
        var preview = $"{_receivedAt:yyyyMMdd}_{normalized}{extension}";
        _finalNamePreviewLabel.Text = "Final file name preview: " + preview;
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
