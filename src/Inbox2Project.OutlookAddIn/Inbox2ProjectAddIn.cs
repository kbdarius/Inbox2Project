using System.Runtime.InteropServices;
using Inbox2Project.Outlook;
using Inbox2Project.Services;

namespace Inbox2Project.OutlookAddIn;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[ProgId("Inbox2Project.OutlookAddIn")]
[Guid("0A6FD82C-4D15-49D4-ABDA-4C0E7BEF7E67")]
public sealed class Inbox2ProjectAddIn : IDTExtensibility2
{
    private delegate void CommandBarButtonClickHandler(object control, ref bool cancelDefault);

    private const string ButtonTag = "Inbox2Project.SaveToInbox2Project";
    private dynamic? _application;
    private dynamic? _button;

    public void OnConnection(object application, ExtensibilityConnectMode connectMode, object addInInstance, ref Array custom)
    {
        _application = application;
        AddContextMenuCommand();
    }

    public void OnDisconnection(ExtensibilityDisconnectMode removeMode, ref Array custom)
    {
        RemoveContextMenuCommand();
        _application = null;
    }

    public void OnAddInsUpdate(ref Array custom) { }
    public void OnStartupComplete(ref Array custom) { }
    public void OnBeginShutdown(ref Array custom) { }

    private void AddContextMenuCommand()
    {
        if (_application is null)
        {
            return;
        }

        dynamic commandBars = _application.ActiveExplorer().CommandBars;
        dynamic contextMenu = commandBars["Context Menu"];
        foreach (dynamic control in contextMenu.Controls)
        {
            if (string.Equals((string)control.Tag, ButtonTag, StringComparison.Ordinal))
            {
                _button = control;
                return;
            }
        }

        _button = contextMenu.Controls.Add(1, Type.Missing, Type.Missing, Type.Missing, true);
        _button.Caption = OutlookContextCommand.SaveCommandName;
        _button.Tag = ButtonTag;
        _button.BeginGroup = true;
        _button.Click += (CommandBarButtonClickHandler)OnButtonClick;
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

    private static void OnButtonClick(object control, ref bool cancelDefault)
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
