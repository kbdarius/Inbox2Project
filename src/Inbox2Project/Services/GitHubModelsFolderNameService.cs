using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Inbox2Project.Services;

/// <summary>
/// Uses the GitHub Models inference API (https://models.github.ai/inference) to suggest
/// folder names. Authentication is via a GitHub Personal Access Token (PAT).
/// This is the recommended path for environments where only GitHub Copilot access is available,
/// as it uses the same GitHub identity/token without requiring VS Code to be running.
/// </summary>
public sealed class GitHubModelsFolderNameService : IAiFolderNameService
{
    public const string PatEnvironmentVariable = "INBOX2PROJECT_GITHUB_PAT";
    public const string DefaultModelName = "gpt-4o-mini";
    private const string ApiBaseUrl = "https://models.github.ai/inference";
    private const string PatCreateUrl = "https://github.com/settings/tokens/new";
    private const int MaxBodyCharacters = 1200;

    private readonly HttpClient _httpClient;

    public GitHubModelsFolderNameService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string ModelName => DefaultModelName;

    public string DownloadUrl => PatCreateUrl;

    public bool IsApiKeyConfigured => !string.IsNullOrWhiteSpace(GetPat());

    public void SaveApiKey(string pat)
    {
        var normalized = pat?.Trim() ?? string.Empty;
        if (normalized.Length < 10
            || (!normalized.StartsWith("ghp_", StringComparison.Ordinal)
                && !normalized.StartsWith("github_pat_", StringComparison.Ordinal)
                && !normalized.StartsWith("gho_", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Enter a valid GitHub Personal Access Token (starts with ghp_, github_pat_, or gho_).",
                nameof(pat));
        }

        Environment.SetEnvironmentVariable(PatEnvironmentVariable, normalized, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable(PatEnvironmentVariable, normalized, EnvironmentVariableTarget.Process);
    }

    public void ClearApiKey()
    {
        Environment.SetEnvironmentVariable(PatEnvironmentVariable, null, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable(PatEnvironmentVariable, null, EnvironmentVariableTarget.Process);
    }

    public async Task<OllamaSetupState> GetSetupStateAsync(CancellationToken cancellationToken = default)
    {
        var pat = GetPat();
        if (string.IsNullOrWhiteSpace(pat))
        {
            return new OllamaSetupState(false, false, false, ModelName, Array.Empty<string>(), DownloadUrl);
        }

        try
        {
            var probePayload = new
            {
                model = ModelName,
                messages = new[]
                {
                    new { role = "user", content = "Reply with the single word OK." },
                },
                max_tokens = 5,
            };
            using var request = CreateRequest(HttpMethod.Post, $"{ApiBaseUrl}/chat/completions", pat);
            request.Content = new StringContent(
                JsonSerializer.Serialize(probePayload),
                Encoding.UTF8,
                "application/json");
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new OllamaSetupState(true, true, true, ModelName, new[] { ModelName }, DownloadUrl);
            }

            // 401/403 = token is invalid
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return new OllamaSetupState(true, true, false, ModelName, Array.Empty<string>(), DownloadUrl);
            }

            // 5xx = service unavailable (but token was accepted)
            if ((int)response.StatusCode >= 500)
            {
                return new OllamaSetupState(true, false, false, ModelName, Array.Empty<string>(), DownloadUrl);
            }

            // Other errors (4xx except 401/403)
            return new OllamaSetupState(true, true, false, ModelName, Array.Empty<string>(), DownloadUrl);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new OllamaSetupState(true, false, false, ModelName, Array.Empty<string>(), DownloadUrl);
        }
        catch (HttpRequestException)
        {
            return new OllamaSetupState(true, false, false, ModelName, Array.Empty<string>(), DownloadUrl);
        }
    }

    public async Task<string?> SuggestFolderNameAsync(string subject, string bodyText, CancellationToken cancellationToken = default)
    {
        var pat = GetPat();
        if (string.IsNullOrWhiteSpace(pat))
        {
            return null;
        }

        var safeSubject = (subject ?? string.Empty).Trim();
        var safeBody = (bodyText ?? string.Empty).Trim();
        if (safeBody.Length > MaxBodyCharacters)
        {
            safeBody = safeBody[..MaxBodyCharacters];
        }

        var payload = new
        {
            model = ModelName,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "Create one concise Windows-safe email file name. Return only the name, with no quotes or explanation. Use letters, numbers, spaces, underscores, periods, and dashes only. Keep the important subject meaning.",
                },
                new
                {
                    role = "user",
                    content = $"Subject: {safeSubject}\nEmail excerpt: {safeBody}",
                },
            },
            max_tokens = 96,
        };

        using var request = CreateRequest(HttpMethod.Post, $"{ApiBaseUrl}/chat/completions", pat);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return NormalizeSuggestion(ReadChoiceText(document.RootElement));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string pat)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        return request;
    }

    private static string? GetPat()
    {
        return Environment.GetEnvironmentVariable(PatEnvironmentVariable, EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable(PatEnvironmentVariable, EnvironmentVariableTarget.User);
    }

    private static string? ReadChoiceText(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content))
            {
                return content.GetString();
            }
        }

        return null;
    }

    private static string? NormalizeSuggestion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var firstLine = value.Trim().Trim('"', '\'', '`')
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(firstLine) ? null : firstLine;
    }
}
