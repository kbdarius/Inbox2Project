using System.Runtime.InteropServices;
using Inbox2Project.Outlook;
using Office = Microsoft.Office.Core;

namespace Inbox2Project.OutlookAddIn;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[ProgId("Inbox2Project.OutlookAddIn")]
[Guid("0A6FD82C-4D15-49D4-ABDA-4C0E7BEF7E67")]
public sealed class Inbox2ProjectAddIn : IDTExtensibility2
{
    private const string ButtonTag = "Inbox2Project.SaveToInbox2Project";
    private global::Microsoft.Office.Interop.Outlook.Application? _outlookApplication;
    private Office.CommandBarButton? _button;

    public void OnConnection(object application, ExtensibilityConnectMode connectMode, object addInInstance, ref Array custom)
    {
        _outlookApplication = (global::Microsoft.Office.Interop.Outlook.Application)application;
    }

    public void OnDisconnection(ExtensibilityDisconnectMode removeMode, ref Array custom)
    {
        RemoveContextMenuCommand();
        if (_outlookApplication is not null)
        {
            _outlookApplication.ItemContextMenuDisplay -= OnItemContextMenuDisplay;
        }

        _outlookApplication = null;
    }

    public void OnAddInsUpdate(ref Array custom) { }
    public void OnStartupComplete(ref Array custom)
    {
        if (_outlookApplication is not null)
        {
            _outlookApplication.ItemContextMenuDisplay += OnItemContextMenuDisplay;
        }
    }
    public void OnBeginShutdown(ref Array custom) { }

    private void OnItemContextMenuDisplay(Office.CommandBar commandBar, global::Microsoft.Office.Interop.Outlook.Selection selection)
    {
        try
        {
            for (var index = 1; index <= commandBar.Controls.Count; index++)
            {
                var control = commandBar.Controls[index];
                if (string.Equals((string)control.Tag, ButtonTag, StringComparison.Ordinal))
                {
                    _button = (Office.CommandBarButton)control;
                    return;
                }
            }

            _button = (Office.CommandBarButton)commandBar.Controls.Add(
                Office.MsoControlType.msoControlButton,
                Type.Missing,
                Type.Missing,
                Type.Missing,
                true);
            _button.Caption = OutlookContextCommand.SaveCommandName;
            _button.Tag = ButtonTag;
            _button.BeginGroup = true;
            _button.Click += OnButtonClick;
        }
        catch
        {
            // Outlook can expose no active explorer during startup. The add-in must
            // fail closed instead of taking Outlook down with it.
            _button = null;
        }
    }

    private void RemoveContextMenuCommand()
    {
        try
        {
            _button?.Delete(true);
        }
        catch
        {
            // Outlook may already be tearing down its command bars.
        }

        _button = null;
    }

    private static void OnButtonClick(Office.CommandBarButton control, ref bool cancelDefault)
    {
        cancelDefault = true;
        var bridgePath = Path.Combine(AppContext.BaseDirectory, "Inbox2Project.OutlookBridge.exe");
        if (!File.Exists(bridgePath))
        {
            System.Windows.Forms.MessageBox.Show(
                "The Inbox2Project Outlook bridge is not installed.",
                "Inbox2Project",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = bridgePath,
            UseShellExecute = true,
        });
    }
}
