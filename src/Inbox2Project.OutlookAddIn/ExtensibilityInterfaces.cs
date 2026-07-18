using System.Runtime.InteropServices;

namespace Inbox2Project.OutlookAddIn;

public enum ExtensibilityConnectMode
{
    ExtCmStartup = 3,
    ExtCmAfterStartup = 0,
}

public enum ExtensibilityDisconnectMode
{
    ExtDmHostShutdown = 0,
    ExtDmUserClosed = 1,
}

[ComVisible(true)]
[Guid("B65AD801-ABAF-11D0-BB8B-00A0C90F2744")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDTExtensibility2
{
    void OnConnection(object application, ExtensibilityConnectMode connectMode, object addInInstance, ref Array custom);
    void OnDisconnection(ExtensibilityDisconnectMode removeMode, ref Array custom);
    void OnAddInsUpdate(ref Array custom);
    void OnStartupComplete(ref Array custom);
    void OnBeginShutdown(ref Array custom);
}
