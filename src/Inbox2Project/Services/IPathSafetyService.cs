namespace Inbox2Project.Services;

public interface IPathSafetyService
{
    string SanitizeName(string value, string fallback = "untitled");

    string GetUniquePath(string directoryPath, string fileName);

    string EnsureSafePathLength(string fullPath);
}
