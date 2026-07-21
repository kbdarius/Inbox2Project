using Inbox2Project.Services;

namespace Inbox2Project.OutlookBridge;

internal sealed class DuplicateSubjectPromptForm : Form
{
    public DuplicateSubjectDecision Decision { get; private set; } = DuplicateSubjectDecision.Cancel;

    public DuplicateSubjectPromptForm(string existingItemName, string subject)
    {
        Text = AppInfo.WindowTitle("Duplicate Email Detected");
        Width = 560;
        Height = 320;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new System.Drawing.Font("Segoe UI", 9F);

        Controls.Add(new Label
        {
            Left = 24,
            Top = 20,
            Width = 500,
            Height = 30,
            Text = "An email with this same subject was already saved:",
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 52,
            Width = 500,
            Height = 42,
            Text = existingItemName,
            AutoEllipsis = true,
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 100,
            Width = 500,
            Height = 30,
            Text = "How would you like to save this email?",
        });

        var useExistingButton = new Button
        {
            Left = 24,
            Top = 140,
            Width = 500,
            Height = 40,
            Text = "Save into the existing folder (overwrite files there)",
        };
        useExistingButton.Click += (_, _) => Complete(DuplicateSubjectDecision.UseExistingFolder);

        var createNewButton = new Button
        {
            Left = 24,
            Top = 188,
            Width = 500,
            Height = 40,
            Text = "Create a new folder (today's date + sender name)",
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
        };
        createNewButton.Click += (_, _) => Complete(DuplicateSubjectDecision.CreateNewFolder);

        var cancelButton = new Button
        {
            Left = 24,
            Top = 240,
            Width = 500,
            Height = 34,
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
        };
        cancelButton.Click += (_, _) => Close();

        Controls.Add(useExistingButton);
        Controls.Add(createNewButton);
        Controls.Add(cancelButton);
        AcceptButton = createNewButton;
        CancelButton = cancelButton;
    }

    private void Complete(DuplicateSubjectDecision decision)
    {
        Decision = decision;
        DialogResult = DialogResult.OK;
        Close();
    }
}
