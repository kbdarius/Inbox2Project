using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Inbox2Project.Services;

public sealed class OpenAiFolderNameService : IAiFolderNameService
{
    public const string ApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    public const string DefaultModelName = "gpt-5-nano";
    private const string ApiBaseUrl = "https://api.openai.com/v1";
    private const string ApiKeysUrl = "https://platform.openai.com/api-keys";
    private const int MaxBodyCharacters = 1200;

    private readonly HttpClient _httpClient;

    public OpenAiFolderNameService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string ModelName => DefaultModelName;

    public string DownloadUrl => ApiKeysUrl;

    public bool IsApiKeyConfigured => !string.IsNullOrWhiteSpace(GetApiKey());

    public void SaveApiKey(string apiKey)
    {
        var normalized = apiKey?.Trim() ?? string.Empty;
        if (!normalized.StartsWith("sk-", StringComparison.Ordinal) || normalized.Length < 20)
        {
            throw new ArgumentException("Enter a valid OpenAI API key beginning with 'sk-'.", nameof(apiKey));
        }

        Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, normalized, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, normalized, EnvironmentVariableTarget.Process);
    }

    public void ClearApiKey()
    {
        Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, null, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, null, EnvironmentVariableTarget.Process);
    }

    public async Task<OllamaSetupState> GetSetupStateAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new OllamaSetupState(false, false, false, ModelName, Array.Empty<string>(), DownloadUrl);
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"{ApiBaseUrl}/models/{ModelName}", apiKey);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new OllamaSetupState(true, true, true, ModelName, new[] { ModelName }, DownloadUrl);
            }

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
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
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
            instructions = "Create one concise Windows-safe email file name. Return only the name, with no quotes or explanation. Use letters, numbers, spaces, underscores, periods, and dashes only. Keep the important subject meaning.",
            input = $"Subject: {safeSubject}\nEmail excerpt: {safeBody}",
            reasoning = new { effort = "minimal" },
            text = new { verbosity = "low" },
            max_output_tokens = 96,
        };

        using var request = CreateRequest(HttpMethod.Post, $"{ApiBaseUrl}/responses", apiKey);
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
            return NormalizeSuggestion(ReadOutputText(document.RootElement));
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

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string apiKey)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    private static string? GetApiKey()
    {
        return Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable, EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable, EnvironmentVariableTarget.User);
    }

    private static string? ReadOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type)
                    && type.GetString() == "output_text"
                    && part.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
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
