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
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IDTExtensibility2
{
    [DispId(1)]
    void OnConnection(object application, ExtensibilityConnectMode connectMode, object addInInstance, ref Array custom);

    [DispId(2)]
    void OnDisconnection(ExtensibilityDisconnectMode removeMode, ref Array custom);

    [DispId(3)]
    void OnAddInsUpdate(ref Array custom);

    [DispId(4)]
    void OnStartupComplete(ref Array custom);

    [DispId(5)]
    void OnBeginShutdown(ref Array custom);
}
