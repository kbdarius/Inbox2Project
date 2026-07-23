using Inbox2Project.Services;

namespace Inbox2Project.OutlookBridge;

internal sealed class GitHubModelsApiKeySetupForm : Form
{
    public GitHubModelsApiKeySetupForm(GitHubModelsFolderNameService service)
    {
        Text = AppInfo.WindowTitle("GitHub Models Setup");
        ClientSize = new System.Drawing.Size(640, 340);
        MinimumSize = new System.Drawing.Size(560, 360);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new System.Drawing.Font("Segoe UI", 9F);
        BackColor = System.Drawing.Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 6,
            BackColor = System.Drawing.Color.White,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        var header = new Label
        {
            Dock = DockStyle.Fill,
            Text = "GitHub Models (Copilot) setup",
            BackColor = System.Drawing.Color.FromArgb(32, 99, 155),
            ForeColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
        };

        var explanation = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Inbox2Project uses GitHub Models (gpt-4o-mini) for file name suggestions. "
                + "Create a GitHub Personal Access Token (PAT) - no special permissions needed - and paste it below. "
                + "The token stays in your Windows user environment variables.",
            ForeColor = System.Drawing.Color.FromArgb(55, 70, 84),
            Padding = new Padding(2, 8, 2, 0),
        };

        var keyLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = service.IsApiKeyConfigured ? "GitHub PAT (a token is already saved):" : "GitHub Personal Access Token:",
            TextAlign = System.Drawing.ContentAlignment.BottomLeft,
        };

        var keyTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = service.IsApiKeyConfigured
                ? "Enter a new token only to replace the saved one"
                : "ghp_... or github_pat_...",
        };

        var statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = service.IsApiKeyConfigured ? System.Drawing.Color.DarkGreen : System.Drawing.Color.DimGray,
            Text = service.IsApiKeyConfigured
                ? "A token is configured for this Windows user. Save a new token to replace it."
                : "Create a PAT at github.com/settings/tokens (classic PAT, no scopes required), then paste it above.",
            Padding = new Padding(2, 8, 2, 0),
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
        };

        var closeButton = new Button { Text = "Close", Width = 88, Height = 34 };
        var saveButton = new Button
        {
            Text = "Save Token",
            Width = 100,
            Height = 34,
            BackColor = System.Drawing.Color.FromArgb(0, 112, 120),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
        };
        var clearButton = new Button { Text = "Clear Saved Token", Width = 136, Height = 34 };
        var createTokenButton = new Button { Text = "Open GitHub Tokens Page", Width = 168, Height = 34 };

        saveButton.Click += (_, _) =>
        {
            var pat = keyTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(pat))
            {
                statusLabel.ForeColor = System.Drawing.Color.DarkRed;
                statusLabel.Text = "Please paste a GitHub PAT before saving.";
                return;
            }

            try
            {
                service.SaveApiKey(pat);
                keyTextBox.Text = string.Empty;
                statusLabel.ForeColor = System.Drawing.Color.DarkGreen;
                statusLabel.Text = "Token saved. AI naming will use GitHub Models on the next run.";
            }
            catch (ArgumentException ex)
            {
                statusLabel.ForeColor = System.Drawing.Color.DarkRed;
                statusLabel.Text = ex.Message;
            }
        };

        clearButton.Click += (_, _) =>
        {
            service.ClearApiKey();
            statusLabel.ForeColor = System.Drawing.Color.DimGray;
            statusLabel.Text = "Saved token cleared.";
        };

        createTokenButton.Click += (_, _) =>
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(service.DownloadUrl) { UseShellExecute = true });
        };

        closeButton.Click += (_, _) => Close();

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(clearButton);
        buttons.Controls.Add(createTokenButton);

        layout.Controls.Add(header);
        layout.Controls.Add(explanation);
        layout.Controls.Add(keyLabel);
        layout.Controls.Add(keyTextBox);
        layout.Controls.Add(statusLabel);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
    }
}
