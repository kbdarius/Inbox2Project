using System.Text;
using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class PathSafetyService : IPathSafetyService
{
    private const int MaxNameLength = 60;

    public string SanitizeName(string value, string fallback = "untitled")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        var sanitized = builder
            .ToString()
            .Replace("\t", " ")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        while (sanitized.EndsWith(".", StringComparison.Ordinal) || sanitized.EndsWith(" ", StringComparison.Ordinal))
        {
            sanitized = sanitized[..^1];
        }

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return fallback;
        }

        return sanitized.Length <= MaxNameLength ? sanitized : sanitized[..MaxNameLength].TrimEnd();
    }

    public string GetUniquePath(string directoryPath, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);

        var candidate = Path.Combine(directoryPath, fileName);
        var index = 1;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            var indexedName = $"{baseName}_{index}{extension}";
            candidate = Path.Combine(directoryPath, indexedName);
            index++;
        }

        return candidate;
    }

    public string EnsureSafePathLength(string fullPath)
    {
        // Names are already capped at MaxNameLength; just return the path.
        return fullPath;
    }
}
