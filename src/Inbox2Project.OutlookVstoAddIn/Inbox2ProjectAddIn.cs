using System.Diagnostics;
using System.Runtime.InteropServices;
using Extensibility;
using Office = Microsoft.Office.Core;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace Inbox2Project.OutlookVstoAddIn;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[ProgId("Inbox2Project.OutlookVstoAddIn")]
[Guid("8A4E9D90-8DB4-4B70-9F4A-3D5B4BB8C4D1")]
public sealed class Inbox2ProjectAddIn : IDTExtensibility2
{
    private const string ButtonTag = "Inbox2Project.SaveToInbox2Project";
    private Outlook.Application? _application;
    private Office.CommandBarButton? _button;

    public void OnConnection(object application, ext_ConnectMode connectMode, object addInInstance, ref Array custom)
    {
        _application = (Outlook.Application)application;
    }

    public void OnStartupComplete(ref Array custom)
    {
        if (_application is not null)
        {
            _application.ItemContextMenuDisplay += OnItemContextMenuDisplay;
        }
    }

    public void OnDisconnection(ext_DisconnectMode removeMode, ref Array custom)
    {
        RemoveButton();
        if (_application is not null)
        {
            _application.ItemContextMenuDisplay -= OnItemContextMenuDisplay;
        }

        _application = null;
    }

    public void OnAddInsUpdate(ref Array custom) { }
    public void OnBeginShutdown(ref Array custom) { }

    private void OnItemContextMenuDisplay(Office.CommandBar commandBar, Outlook.Selection selection)
    {
        try
        {
            for (var index = 1; index <= commandBar.Controls.Count; index++)
            {
                var control = commandBar.Controls[index];
                if (string.Equals(control.Tag, ButtonTag, StringComparison.Ordinal))
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
            _button.Caption = "Save to Inbox2Project";
            _button.Tag = ButtonTag;
            _button.BeginGroup = true;
            _button.Click += OnButtonClick;
        }
        catch
        {
            _button = null;
        }
    }

    private static void OnButtonClick(Office.CommandBarButton control, ref bool cancelDefault)
    {
        cancelDefault = true;
        var addInDirectory = Path.GetDirectoryName(typeof(Inbox2ProjectAddIn).Assembly.Location)
            ?? AppDomain.CurrentDomain.BaseDirectory;
        var bridgePath = Path.Combine(addInDirectory, "Inbox2Project.OutlookBridge.exe");
        if (!File.Exists(bridgePath))
        {
            System.Windows.Forms.MessageBox.Show(
                "The Inbox2Project Outlook bridge is not installed beside the add-in.",
                "Inbox2Project",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = bridgePath,
            WorkingDirectory = addInDirectory,
            UseShellExecute = true,
        });
    }

    private void RemoveButton()
    {
        try
        {
            _button?.Delete(true);
        }
        catch
        {
            // Outlook may already be shutting down.
        }

        _button = null;
    }
}
