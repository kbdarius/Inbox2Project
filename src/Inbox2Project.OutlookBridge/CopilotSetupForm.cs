using System.Diagnostics;

namespace Inbox2Project.OutlookBridge;

internal sealed class CopilotSetupForm : Form
{
    private readonly CopilotFolderNameService _service;
    private readonly Label _statusLabel;
    private readonly Button _checkButton;

    public CopilotSetupForm(CopilotFolderNameService service)
    {
        _service = service;

        Text = AppInfo.WindowTitle("GitHub Copilot Setup");
        ClientSize = new System.Drawing.Size(650, 330);
        MinimumSize = new System.Drawing.Size(650, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new System.Drawing.Font("Segoe UI", 9F);
        BackColor = System.Drawing.Color.White;

        var header = new Label
        {
            Left = 20,
            Top = 18,
            Width = 610,
            Height = 34,
            Text = "GitHub Copilot account",
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(32, 99, 155),
        };
        var explanation = new Label
        {
            Left = 20,
            Top = 58,
            Width = 610,
            Height = 100,
            Text = "Inbox2Project uses the GitHub account already signed in to Copilot CLI. It does not store a GitHub token or API key. When selected, the email subject and up to 1,200 characters of its body are sent to GitHub Copilot for a suggested name. Requests use the Copilot usage allowance for that account and plan.",
            AutoSize = false,
        };
        var signInInstructions = new Label
        {
            Left = 20,
            Top = 164,
            Width = 610,
            Height = 42,
            Text = "If you are not signed in, run `copilot` in a terminal, complete GitHub sign-in, then check again.",
            AutoSize = false,
        };
        _statusLabel = new Label
        {
            Left = 20,
            Top = 214,
            Width = 610,
            Height = 42,
            Text = "Checking GitHub Copilot sign-in...",
            AutoSize = false,
        };

        _checkButton = new Button
        {
            Left = 20,
            Top = 270,
            Width = 130,
            Height = 34,
            Text = "Check Sign-In",
        };
        _checkButton.Click += async (_, _) => await CheckStatusAsync();

        var docsButton = new Button
        {
            Left = 160,
            Top = 270,
            Width = 210,
            Height = 34,
            Text = "Copilot SDK Sign-In Guide",
        };
        docsButton.Click += (_, _) => Process.Start(
            new ProcessStartInfo(_service.SetupUrl) { UseShellExecute = true });

        var closeButton = new Button
        {
            Left = 552,
            Top = 270,
            Width = 78,
            Height = 34,
            Text = "Close",
        };
        closeButton.Click += (_, _) => Close();

        Controls.Add(header);
        Controls.Add(explanation);
        Controls.Add(signInInstructions);
        Controls.Add(_statusLabel);
        Controls.Add(_checkButton);
        Controls.Add(docsButton);
        Controls.Add(closeButton);

        AcceptButton = _checkButton;
        CancelButton = closeButton;
        Load += async (_, _) => await CheckStatusAsync();
    }

    private async Task CheckStatusAsync()
    {
        _checkButton.Enabled = false;
        _statusLabel.ForeColor = System.Drawing.Color.FromArgb(32, 99, 155);
        _statusLabel.Text = "Checking GitHub Copilot sign-in...";

        try
        {
            var state = await _service.GetSetupStateAsync();
            if (!state.IsServiceAvailable)
            {
                SetStatus(
                    state.StatusMessage ?? "The GitHub Copilot runtime could not be started.",
                    System.Drawing.Color.DarkRed);
                return;
            }

            if (!state.IsConfigured)
            {
                SetStatus(
                    state.StatusMessage ?? "Not signed in. Run `copilot` in a terminal and complete GitHub sign-in.",
                    System.Drawing.Color.DarkRed);
                return;
            }

            if (!state.IsModelAvailable)
            {
                SetStatus(
                    state.StatusMessage ?? "The signed-in account does not currently have Copilot model access.",
                    System.Drawing.Color.DarkRed);
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.StatusMessage))
            {
                SetStatus(state.StatusMessage, System.Drawing.Color.DarkRed);
                return;
            }

            SetStatus(
                "Signed in. Copilot naming is ready and will use this account's current usage allowance.",
                System.Drawing.Color.DarkGreen);
        }
        finally
        {
            _checkButton.Enabled = true;
        }
    }

    private void SetStatus(string message, System.Drawing.Color color)
    {
        _statusLabel.ForeColor = color;
        _statusLabel.Text = message;
    }
}
