using System.Diagnostics;

namespace Inbox2Project.OutlookBridge;

internal sealed class CompletionForm : Form
{
    public CompletionForm(string message, IReadOnlyList<string> outputPaths)
    {
        var folder = outputPaths.Count > 0 ? Path.GetDirectoryName(outputPaths[0]) : null;
        Text = AppInfo.WindowTitle("Save Complete");
        Width = 540;
        Height = 260;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new System.Drawing.Font("Segoe UI", 9F);

        Controls.Add(new Label { Left = 28, Top = 24, Width = 470, Height = 30, Text = "Saved successfully", Font = new System.Drawing.Font(Font.FontFamily, 14F, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.Color.FromArgb(30, 110, 70) });
        Controls.Add(new Label { Left = 28, Top = 68, Width = 470, Height = 42, Text = message });
        var pathLink = new LinkLabel { Left = 28, Top = 116, Width = 470, Height = 42, Text = folder ?? "Saved folder unavailable", AutoEllipsis = true, Enabled = folder is not null };
        pathLink.LinkClicked += (_, _) =>
        {
            if (folder is not null) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        };
        var openButton = new Button { Left = 278, Top = 174, Width = 130, Height = 34, Text = "Open folder", Enabled = folder is not null };
        openButton.Click += (_, _) => { if (folder is not null) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true }); };
        var doneButton = new Button { Left = 418, Top = 174, Width = 80, Height = 34, Text = "Done", DialogResult = DialogResult.OK };
        Controls.Add(pathLink);
        Controls.Add(openButton);
        Controls.Add(doneButton);
        AcceptButton = doneButton;
    }
}
