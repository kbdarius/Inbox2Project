using System.Diagnostics;
using System.Runtime.InteropServices;
using Extensibility;
using Office = Microsoft.Office.Core;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace Inbox2Project.OutlookVstoAddIn;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDispatch)]
[ProgId("Inbox2Project.OutlookVstoAddIn")]
[Guid("8A4E9D90-8DB4-4B70-9F4A-3D5B4BB8C4D1")]
public sealed class Inbox2ProjectAddIn : IDTExtensibility2, Office.IRibbonExtensibility
{
    private const string ExplorerRibbonId = "Microsoft.Outlook.Explorer";

    private const string ExplorerRibbonXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<customUI xmlns=""http://schemas.microsoft.com/office/2009/07/customui"">
  <contextMenus>
    <contextMenu idMso=""ContextMenuMailItem"">
      <button id=""Inbox2Project.SaveMailItem""
              label=""Save to Inbox2Project""
              imageMso=""FileSaveAs""
              insertBeforeMso=""Copy""
              onAction=""OnSaveToInbox2Project"" />
    </contextMenu>
    <contextMenu idMso=""ContextMenuMultipleItems"">
      <button id=""Inbox2Project.SaveMultipleItems""
              label=""Save to Inbox2Project""
              imageMso=""FileSaveAs""
              insertBeforeMso=""Copy""
              onAction=""OnSaveToInbox2Project"" />
    </contextMenu>
  </contextMenus>
</customUI>";

    public void OnConnection(object application, ext_ConnectMode connectMode, object addInInstance, ref Array custom) { }
    public void OnStartupComplete(ref Array custom) { }
    public void OnDisconnection(ext_DisconnectMode removeMode, ref Array custom) { }
    public void OnAddInsUpdate(ref Array custom) { }
    public void OnBeginShutdown(ref Array custom) { }

    public string GetCustomUI(string ribbonId)
    {
        return string.Equals(ribbonId, ExplorerRibbonId, StringComparison.Ordinal)
            ? ExplorerRibbonXml
            : string.Empty;
    }

    public void OnSaveToInbox2Project(Office.IRibbonControl control)
    {
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
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }
}
