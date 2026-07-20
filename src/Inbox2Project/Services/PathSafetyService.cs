using System.Text;
using System.Text.RegularExpressions;
using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class PathSafetyService : IPathSafetyService
{
    private const int MaxNameLength = 60;
    private static readonly Regex PrefixCleaner = new(@"^\s*(?:(?:re|fw|fwd)\s*:?\s*)+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MultiDelimiterRegex = new(@"[\\/\|:*?\" + "\"" + @"<>[\]{}()]+", RegexOptions.Compiled);
    private static readonly Regex WordSeparatorRegex = new(@"\s+", RegexOptions.Compiled);

    public string SanitizeName(string value, string fallback = "untitled")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var cleanedPrefix = PrefixCleaner.Replace(value.Trim(), string.Empty);
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in cleanedPrefix)
        {
            if (invalid.Contains(ch) || ch is '/' or '\\')
            {
                builder.Append(' ');
            }
            else if (char.IsControl(ch))
            {
                builder.Append(' ');
            }
            else
            {
                builder.Append(ch);
            }
        }

        var sanitized = builder.ToString();
        sanitized = MultiDelimiterRegex.Replace(sanitized, " ");
        sanitized = sanitized.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        sanitized = WordSeparatorRegex.Replace(sanitized.Trim(), "_");

        while (sanitized.EndsWith(".", StringComparison.Ordinal) || sanitized.EndsWith("_", StringComparison.Ordinal))
        {
            sanitized = sanitized[..^1];
        }

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return fallback;
        }

        return sanitized.Length <= MaxNameLength ? sanitized : sanitized[..MaxNameLength].Trim('_');
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
