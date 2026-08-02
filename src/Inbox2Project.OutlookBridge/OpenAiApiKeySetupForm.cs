using Inbox2Project.Services;

namespace Inbox2Project.OutlookBridge;

internal sealed class OpenAiApiKeySetupForm : Form
{
    public OpenAiApiKeySetupForm(OpenAiFolderNameService service)
    {
        Text = AppInfo.WindowTitle("OpenAI API Setup");
        ClientSize = new System.Drawing.Size(760, 390);
        MinimumSize = new System.Drawing.Size(700, 420);
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
            RowCount = 8,
            BackColor = System.Drawing.Color.White,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

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

        var statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = service.IsApiKeyConfigured ? System.Drawing.Color.DarkGreen : System.Drawing.Color.DimGray,
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

        var closeButton = new Button { Text = "Close", Width = 88, Height = 34 };
        var saveButton = new Button
        {
            Text = "Save Settings",
            Width = 116,
            Height = 34,
            BackColor = System.Drawing.Color.FromArgb(0, 112, 120),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
        };
        var clearButton = new Button { Text = "Clear Saved Key", Width = 126, Height = 34 };
        var createKeyButton = new Button { Text = "Open API Keys Page", Width = 142, Height = 34 };
        var testButton = new Button { Text = "Test Models", Width = 112, Height = 34 };

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
                statusLabel.Text = string.Join("   |   ", results.Select(result =>
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

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(testButton);
        buttons.Controls.Add(clearButton);
        buttons.Controls.Add(createKeyButton);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(explanation, 0, 1);
        layout.Controls.Add(modelLabel, 0, 2);
        layout.Controls.Add(modelComboBox, 0, 3);
        layout.Controls.Add(keyLabel, 0, 4);
        layout.Controls.Add(keyTextBox, 0, 5);
        layout.Controls.Add(statusLabel, 0, 6);
        layout.Controls.Add(buttons, 0, 7);
        Controls.Add(layout);

        AcceptButton = saveButton;
        CancelButton = closeButton;
    }
}
