using Inbox2Project.Services;

namespace Inbox2Project.OutlookBridge;

internal sealed class OpenAiApiKeySetupForm : Form
{
    public OpenAiApiKeySetupForm(OpenAiFolderNameService service)
    {
        Text = AppInfo.WindowTitle("OpenAI API Setup");
        ClientSize = new System.Drawing.Size(760, 460);
        MinimumSize = new System.Drawing.Size(700, 490);
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
            RowCount = 11,
            BackColor = System.Drawing.Color.White,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        var billingService = new OpenAiBillingService();
        var billingState = billingService.LoadState();

        var header = new Label
        {
            Dock = DockStyle.Fill,
            Text = "OpenAI API setup",
            BackColor = System.Drawing.Color.FromArgb(32, 99, 155),
            ForeColor = System.Drawing.Color.White,
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
        };

        var explanation = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Choose a low-cost OpenAI model for short file names. Your model choice and API key are saved only for this Windows user.",
            ForeColor = System.Drawing.Color.FromArgb(55, 70, 84),
            Padding = new Padding(2, 8, 2, 0),
        };

        var modelLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "OpenAI model:",
            TextAlign = System.Drawing.ContentAlignment.BottomLeft,
        };

        var modelComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.System,
        };
        modelComboBox.Items.AddRange(OpenAiFolderNameService.SupportedModelNames.Cast<object>().ToArray());
        modelComboBox.SelectedItem = service.ModelName;
        if (modelComboBox.SelectedIndex < 0)
        {
            modelComboBox.SelectedIndex = 0;
        }

        var keyLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = service.IsApiKeyConfigured ? "API key (a key is already saved):" : "API key:",
            TextAlign = System.Drawing.ContentAlignment.BottomLeft,
        };

        var keyTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = service.IsApiKeyConfigured ? "Enter a new key only to replace the saved key" : "sk-...",
        };

        var adminKeyLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Billing Admin API key (stored encrypted for this Windows user):",
            TextAlign = System.Drawing.ContentAlignment.BottomLeft,
        };

        var adminKeyTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = billingState.IsAdminApiKeyConfigured ? "Enter a new Admin key only to replace the saved key" : "sk-admin-...",
        };

        var billingRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(2, 4, 0, 0),
        };
        var balanceLabel = new Label
        {
            Text = "Starting balance ($):",
            AutoSize = true,
            Margin = new Padding(0, 7, 6, 0),
        };
        var balanceTextBox = new TextBox
        {
            Width = 88,
            Height = 26,
            Text = billingState.StartingBalance.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 3, 8, 0),
        };
        var refreshBalanceButton = new Button
        {
            Text = "Refresh billing data",
            Width = 140,
            Height = 30,
            Margin = new Padding(0, 1, 12, 0),
        };
        var billingStatusLabel = new Label
        {
            AutoSize = false,
            Width = 310,
            Height = 32,
            ForeColor = System.Drawing.Color.FromArgb(55, 70, 84),
            Text = billingState.LastRefreshedUtc is null
                ? "Enter the Admin key, then refresh."
                : $"Estimated remaining: ${Math.Max(0m, billingState.StartingBalance - billingState.SpentSinceBaseline):0.00} | Last updated {billingState.LastRefreshedUtc.Value.ToLocalTime():g}",
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        };
        billingRow.Controls.Add(balanceLabel);
        billingRow.Controls.Add(balanceTextBox);
        billingRow.Controls.Add(refreshBalanceButton);
        billingRow.Controls.Add(billingStatusLabel);

        var statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = service.IsApiKeyConfigured ? System.Drawing.Color.DarkGreen : System.Drawing.Color.DimGray,
            AutoEllipsis = true,
            Text = service.IsApiKeyConfigured
                ? "A key is configured for this Windows user. Save a new key to replace it."
                : "Create an API key on the OpenAI platform, then paste it above.",
            Padding = new Padding(2, 8, 2, 0),
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
        };

        var closeButton = new Button { Text = "Close", Width = 78, Height = 34 };
        var saveButton = new Button
        {
            Text = "Save Settings",
            Width = 108,
            Height = 34,
            BackColor = System.Drawing.Color.FromArgb(0, 112, 120),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
        };
        var clearButton = new Button { Text = "Clear Key", Width = 92, Height = 34 };
        var createKeyButton = new Button { Text = "API Keys Page", Width = 112, Height = 34 };
        var billingButton = new Button { Text = "Check API Balance", Width = 126, Height = 34 };
        var testButton = new Button { Text = "Test Models", Width = 104, Height = 34 };

        closeButton.Click += (_, _) => Close();
        saveButton.Click += (_, _) =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(keyTextBox.Text))
                {
                    service.SaveApiKey(keyTextBox.Text);
                }
                else if (!service.IsApiKeyConfigured)
                {
                    throw new InvalidOperationException("Enter an OpenAI API key before saving.");
                }

                service.SaveModelName(modelComboBox.SelectedItem?.ToString() ?? string.Empty);
                if (!decimal.TryParse(balanceTextBox.Text, System.Globalization.NumberStyles.Currency, System.Globalization.CultureInfo.CurrentCulture, out var startingBalance)
                    || startingBalance < 0)
                {
                    throw new InvalidOperationException("Enter a valid non-negative starting balance.");
                }

                if (string.Equals(adminKeyTextBox.Text, string.Empty, StringComparison.Ordinal) == false)
                {
                    billingService.SaveAdminApiKey(adminKeyTextBox.Text);
                }

                if (billingState.BaselineUtc is null || startingBalance != billingState.StartingBalance)
                {
                    billingService.SetStartingBalance(startingBalance);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception)
            {
                statusLabel.ForeColor = System.Drawing.Color.DarkRed;
                statusLabel.Text = exception.Message;
            }
        };
        clearButton.Click += (_, _) =>
        {
            service.ClearApiKey();
            keyTextBox.Clear();
            statusLabel.ForeColor = System.Drawing.Color.DarkGreen;
            statusLabel.Text = "The saved OpenAI API key was removed.";
        };
        createKeyButton.Click += (_, _) => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(service.DownloadUrl) { UseShellExecute = true });
        billingButton.Click += (_, _) => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(service.BillingUrl) { UseShellExecute = true });
        testButton.Click += async (_, _) =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(keyTextBox.Text))
                {
                    service.SaveApiKey(keyTextBox.Text);
                }

                if (!service.IsApiKeyConfigured)
                {
                    throw new InvalidOperationException("Enter and save an OpenAI API key before testing.");
                }

                testButton.Enabled = false;
                statusLabel.ForeColor = System.Drawing.Color.FromArgb(32, 99, 155);
                statusLabel.Text = "Testing access to each model...";

                var results = new List<OpenAiModelTestResult>();
                foreach (var modelName in OpenAiFolderNameService.SupportedModelNames)
                {
                    results.Add(await service.TestModelAsync(modelName));
                }

                statusLabel.ForeColor = results.All(result => result.IsConnected)
                    ? System.Drawing.Color.DarkGreen
                    : System.Drawing.Color.DarkRed;
                statusLabel.Text = "Model tests: " + string.Join("   |   ", results.Select(result =>
                    $"{result.ModelName}: {result.Message}"));
            }
            catch (Exception exception)
            {
                statusLabel.ForeColor = System.Drawing.Color.DarkRed;
                statusLabel.Text = exception.Message;
            }
            finally
            {
                testButton.Enabled = true;
            }
        };
        refreshBalanceButton.Click += async (_, _) =>
        {
            try
            {
                if (!decimal.TryParse(balanceTextBox.Text, System.Globalization.NumberStyles.Currency, System.Globalization.CultureInfo.CurrentCulture, out var startingBalance)
                    || startingBalance < 0)
                {
                    throw new InvalidOperationException("Enter a valid non-negative starting balance.");
                }

                if (!string.IsNullOrWhiteSpace(adminKeyTextBox.Text))
                {
                    billingService.SaveAdminApiKey(adminKeyTextBox.Text);
                }

                billingState = billingService.LoadState();
                if (billingState.BaselineUtc is null || startingBalance != billingState.StartingBalance)
                {
                    billingService.SetStartingBalance(startingBalance);
                }

                refreshBalanceButton.Enabled = false;
                billingStatusLabel.ForeColor = System.Drawing.Color.FromArgb(32, 99, 155);
                billingStatusLabel.Text = "Refreshing billing data...";
                var result = await billingService.RefreshAsync();
                billingState = billingService.LoadState();
                billingStatusLabel.ForeColor = result.IsSuccess
                    ? System.Drawing.Color.DarkGreen
                    : System.Drawing.Color.DarkRed;
                billingStatusLabel.Text = result.IsSuccess
                    ? $"Estimated remaining: ${result.EstimatedRemaining:0.00} | Spent: ${result.SpentSinceBaseline:0.0000}"
                    : result.Message;
            }
            catch (Exception exception)
            {
                billingStatusLabel.ForeColor = System.Drawing.Color.DarkRed;
                billingStatusLabel.Text = exception.Message;
            }
            finally
            {
                refreshBalanceButton.Enabled = true;
            }
        };

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(testButton);
        buttons.Controls.Add(clearButton);
        buttons.Controls.Add(createKeyButton);
        buttons.Controls.Add(billingButton);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(explanation, 0, 1);
        layout.Controls.Add(modelLabel, 0, 2);
        layout.Controls.Add(modelComboBox, 0, 3);
        layout.Controls.Add(keyLabel, 0, 4);
        layout.Controls.Add(keyTextBox, 0, 5);
        layout.Controls.Add(adminKeyLabel, 0, 6);
        layout.Controls.Add(adminKeyTextBox, 0, 7);
        layout.Controls.Add(billingRow, 0, 8);
        layout.Controls.Add(statusLabel, 0, 9);
        layout.Controls.Add(buttons, 0, 10);
        Controls.Add(layout);

        AcceptButton = saveButton;
        CancelButton = closeButton;
    }
}
