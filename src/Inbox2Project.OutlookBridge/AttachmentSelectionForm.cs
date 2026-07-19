using Inbox2Project.Models;

namespace Inbox2Project.OutlookBridge;

internal sealed class AttachmentSelectionForm : Form
{
    private readonly CheckedListBox _items;
    private readonly Button _attachmentsOnlyButton;
    private readonly Button _emailAndAttachmentsButton;

    public AttachmentSelectionForm(IReadOnlyList<AttachmentData> attachments)
    {
        Text = AppInfo.WindowTitle("Choose Attachments");
        Width = 700;
        Height = 390;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new System.Drawing.Font("Segoe UI", 9F);

        Controls.Add(new Label { Left = 24, Top = 20, Width = 630, Height = 42, Text = "Select the files to save. Signature and inline images are unchecked automatically." });
        _items = new CheckedListBox { Left = 24, Top = 70, Width = 630, Height = 210, CheckOnClick = true };
        foreach (var attachment in attachments)
        {
            _items.Items.Add(attachment, !attachment.IsInline);
        }

        var emailOnlyButton = new Button { Left = 24, Top = 300, Width = 145, Height = 34, Text = "Save email only" };
        emailOnlyButton.Click += (_, _) => Complete(true, Array.Empty<AttachmentData>());
        _attachmentsOnlyButton = new Button { Left = 179, Top = 300, Width = 145, Height = 34, Text = "Save attachments only" };
        _attachmentsOnlyButton.Click += (_, _) => Complete(false, GetCheckedAttachments());
        _emailAndAttachmentsButton = new Button { Left = 334, Top = 300, Width = 165, Height = 34, Text = "Email + attachments", Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold) };
        _emailAndAttachmentsButton.Click += (_, _) => Complete(true, GetCheckedAttachments());
        var cancelButton = new Button { Left = 509, Top = 300, Width = 145, Height = 34, Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancelButton.Click += (_, _) => Close();
        _items.ItemCheck += (_, _) => BeginInvoke(UpdateActionButtons);

        Controls.Add(_items);
        Controls.Add(emailOnlyButton);
        Controls.Add(_attachmentsOnlyButton);
        Controls.Add(_emailAndAttachmentsButton);
        Controls.Add(cancelButton);
        AcceptButton = _emailAndAttachmentsButton;
        CancelButton = cancelButton;
        UpdateActionButtons();
    }

    public AttachmentSaveChoice Choice { get; private set; } = new(true, false, Array.Empty<AttachmentData>());

    private IReadOnlyList<AttachmentData> GetCheckedAttachments() => _items.CheckedItems.Cast<AttachmentData>().ToList();

    private void Complete(bool includeEmail, IReadOnlyList<AttachmentData> attachments)
    {
        Choice = new AttachmentSaveChoice(false, includeEmail, attachments);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateActionButtons()
    {
        var hasSelection = _items.CheckedItems.Count > 0;
        _attachmentsOnlyButton.Enabled = hasSelection;
        _emailAndAttachmentsButton.Enabled = hasSelection;
    }
}
