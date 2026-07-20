using System.Net.Http.Json;
using System.Text.Json;

namespace Inbox2Project.Services;

public sealed class OllamaFolderNameService : IAiFolderNameService
{
    private const string DefaultOllamaBaseUrl = "http://127.0.0.1:11434";
    private const string DefaultModelName = "phi3-small";
    private const string DefaultDownloadUrl = "https://ollama.com/download";
    private const int PromptBodyLimit = 1200;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _modelName;

    public OllamaFolderNameService(HttpClient httpClient, string modelName = DefaultModelName, string ollamaBaseUrl = DefaultOllamaBaseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = ollamaBaseUrl.TrimEnd('/');
        _modelName = modelName;
    }

    public string ModelName => _modelName;

    public string DownloadUrl => DefaultDownloadUrl;

    public async Task<OllamaSetupState> GetSetupStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new OllamaSetupState(false, false);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
            {
                return new OllamaSetupState(false, false);
            }

            foreach (var modelElement in modelsElement.EnumerateArray())
            {
                if (!modelElement.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (name.Equals(_modelName, StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith($"{_modelName}:", StringComparison.OrdinalIgnoreCase))
                {
                    return new OllamaSetupState(true, true);
                }
            }

            return new OllamaSetupState(true, false);
        }
        catch (HttpRequestException)
        {
            return new OllamaSetupState(false, false);
        }
    }

    public async Task<string?> SuggestFolderNameAsync(string subject, string bodyText, CancellationToken cancellationToken = default)
    {
        var setup = await GetSetupStateAsync(cancellationToken);
        if (!setup.IsServerAvailable || !setup.IsModelAvailable)
        {
            return null;
        }

        var prompt = CreatePrompt(subject, bodyText);
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/generate",
                new
                {
                    model = _modelName,
                    prompt,
                    stream = false,
                    options = new { temperature = 0.0 },
                },
                JsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions, cancellationToken);
            if (result is null)
            {
                return null;
            }

            using var doc = result;
            if (!doc.RootElement.TryGetProperty("response", out var responseElement))
            {
                return null;
            }

            var value = responseElement.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return NormalizeSuggestion(value);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static string CreatePrompt(string subject, string bodyText)
    {
        var safeSubject = (subject ?? string.Empty).Trim();
        var safeBody = (bodyText ?? string.Empty).Trim();
        if (safeBody.Length > PromptBodyLimit)
        {
            safeBody = safeBody[..PromptBodyLimit];
        }

        return
            $"You are naming email exports. Suggest ONE folder/file base name using letters, numbers, spaces, and dashes only. " +
            $"Return only a single line, 3-60 characters, no path separators. Subject: {safeSubject} Body: {safeBody}";
    }

    private static string? NormalizeSuggestion(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var line = raw.Replace("\r", " ").Replace("\n", " ").Trim();
        var firstLine = line.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            firstLine = line;
        }

        var cleaned = firstLine
            .Replace('\"', ' ')
            .Replace('\'', ' ')
            .Replace('`', ' ')
            .Replace("[", " ")
            .Replace("]", " ")
            .Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        return cleaned;
    }
}

public sealed class NoOpAiFolderNameService : IAiFolderNameService
{
    public string ModelName => "phi3-small";

    public string DownloadUrl => "https://ollama.com/download";

    public Task<OllamaSetupState> GetSetupStateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new OllamaSetupState(false, false));

    public Task<string?> SuggestFolderNameAsync(string subject, string bodyText, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
