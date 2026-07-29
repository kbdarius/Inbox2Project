using System.Text.RegularExpressions;

namespace Inbox2Project.Services;

public static class FileNameBaseNameNormalizer
{
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly HashSet<string> TrailingExtensionTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".msg",
        ".eml",
        ".doc",
        ".docx",
        ".pdf",
        ".xls",
        ".xlsx",
        ".csv",
        ".ppt",
        ".pptx",
        ".zip",
        ".7z",
        ".rar",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".tif",
        ".tiff",
        ".webp",
        ".heic",
    };

    public static string NormalizeEditableBaseName(
        string? value,
        string fallback,
        DateTimeOffset receivedAt,
        IPathSafetyService pathSafetyService)
    {
        var original = string.IsNullOrWhiteSpace(value) ? fallback : value!;
        var stripped = StripAutoDateFromBaseName(original, receivedAt);
        return pathSafetyService.SanitizeName(stripped, fallback);
    }

    public static string StripAutoDateFromBaseName(string value, DateTimeOffset receivedAt)
    {
        var trimmed = MultiSpaceRegex.Replace(value.Trim(), " ");
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        var receivedDate = receivedAt.Date;
        var exactTokens = new[]
        {
            receivedDate.ToString("yyyyMMdd"),
            receivedDate.ToString("yyyy-MM-dd"),
            receivedDate.ToString("yyyy_MM_dd"),
            receivedDate.ToString("MM-dd-yyyy"),
            receivedDate.ToString("MM_dd_yyyy"),
            receivedDate.ToString("M-d-yyyy"),
            receivedDate.ToString("M_d_yyyy"),
        };

        foreach (var token in exactTokens)
        {
            trimmed = RemoveDuplicateDateToken(trimmed, token);
        }

        trimmed = RemoveTrailingFileExtensionToken(trimmed);
        return MultiSpaceRegex.Replace(trimmed.Trim(' ', '_', '-'), " ");
    }

    private static string RemoveTrailingFileExtensionToken(string value)
    {
        var working = value.Trim();
        while (true)
        {
            var extension = Path.GetExtension(working);
            if (string.IsNullOrWhiteSpace(extension)
                || !TrailingExtensionTokens.Contains(extension)
                || working.Length <= extension.Length)
            {
                return working;
            }

            working = working[..^extension.Length].TrimEnd(' ', '_', '-', '.');
        }
    }

    private static string RemoveDuplicateDateToken(string value, string token)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(token))
        {
            return value;
        }

        var working = value.Trim();
        if (working.StartsWith(token + "_", StringComparison.OrdinalIgnoreCase)
            || working.StartsWith(token + "-", StringComparison.OrdinalIgnoreCase)
            || working.StartsWith(token + " ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(working, token, StringComparison.OrdinalIgnoreCase))
        {
            working = working[token.Length..].TrimStart(' ', '_', '-');
        }

        if (working.EndsWith("_" + token, StringComparison.OrdinalIgnoreCase)
            || working.EndsWith("-" + token, StringComparison.OrdinalIgnoreCase)
            || working.EndsWith(" " + token, StringComparison.OrdinalIgnoreCase))
        {
            working = working[..^token.Length].TrimEnd(' ', '_', '-');
        }

        return working;
    }
}
