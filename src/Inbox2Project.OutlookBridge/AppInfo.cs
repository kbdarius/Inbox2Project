using System.Reflection;

namespace Inbox2Project.OutlookBridge;

internal static class AppInfo
{
    public static string Version { get; } = GetVersion();

    public static string WindowTitle(string page) => $"Inbox2Project v{Version} - {page}";

    private static string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
