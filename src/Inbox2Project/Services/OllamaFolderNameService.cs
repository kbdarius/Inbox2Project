using System.Net.Http.Json;
using System.Text.Json;

namespace Inbox2Project.Services;

public sealed class OllamaFolderNameService : IAiFolderNameService
{
    private const string DefaultOllamaBaseUrl = "http://127.0.0.1:11434";
    private const string DefaultModelName = "phi3:mini";
    private const string DefaultDownloadUrl = "https://ollama.com/download";
    private const string DefaultSetupGuideUrl = "https://ollama.com/library/phi3";
    private const int PromptBodyLimit = 1200;
    private static readonly string[] PreferredModels =
    {
        "phi3:mini",
        "phi3",
        "qwen2.5:0.5b",
        "tinyllama",
    };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _preferredModelName;
    private string? _resolvedModelName;

    public OllamaFolderNameService(HttpClient httpClient, string modelName = DefaultModelName, string ollamaBaseUrl = DefaultOllamaBaseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = ollamaBaseUrl.TrimEnd('/');
        _preferredModelName = modelName;
    }

    public string ModelName => _resolvedModelName ?? _preferredModelName;

    public string DownloadUrl => DefaultDownloadUrl;

    public async Task<OllamaSetupState> GetSetupStateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsOllamaInstalledOnMachine())
        {
            _resolvedModelName = null;
            return new OllamaSetupState(false, false, false, null, Array.Empty<string>(), DefaultDownloadUrl);
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _resolvedModelName = null;
                return new OllamaSetupState(true, false, false, null, Array.Empty<string>(), DefaultDownloadUrl);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
            {
                _resolvedModelName = null;
                return new OllamaSetupState(true, true, false, null, Array.Empty<string>(), DefaultSetupGuideUrl);
            }

            var installedModels = new List<string>();

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

                installedModels.Add(name);
            }

            var selectedModel = ResolvePreferredModel(installedModels);
            _resolvedModelName = selectedModel;
            if (!string.IsNullOrWhiteSpace(selectedModel))
            {
                return new OllamaSetupState(true, true, true, selectedModel, installedModels, DefaultSetupGuideUrl);
            }

            return new OllamaSetupState(true, true, false, null, installedModels, DefaultSetupGuideUrl);
        }
        catch (HttpRequestException)
        {
            _resolvedModelName = null;
            return new OllamaSetupState(true, false, false, null, Array.Empty<string>(), DefaultDownloadUrl);
        }
    }

    public async Task<string?> SuggestFolderNameAsync(string subject, string bodyText, CancellationToken cancellationToken = default)
    {
        var setup = await GetSetupStateAsync(cancellationToken);
        if (!setup.IsServerAvailable || !setup.IsModelAvailable || string.IsNullOrWhiteSpace(setup.SelectedModelName))
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
                    model = setup.SelectedModelName,
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

    private static string? ResolvePreferredModel(IReadOnlyList<string> installedModels)
    {
        if (installedModels.Count == 0)
        {
            return null;
        }

        foreach (var preferred in PreferredModels)
        {
            var match = installedModels.FirstOrDefault(name =>
                name.Equals(preferred, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith($"{preferred}:", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
            {
                return match;
            }
        }

        var preferredFamily = installedModels.FirstOrDefault(name =>
            name.StartsWith("phi3", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("qwen", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("tinyllama", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(preferredFamily))
        {
            return preferredFamily;
        }

        return installedModels[0];
    }

    private static bool IsOllamaInstalledOnMachine()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidatePaths = new[]
        {
            Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe"),
            Path.Combine(userProfile, ".ollama"),
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                return true;
            }
        }

        return false;
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
            $"Return only a single line, 3-255 characters, no path separators. Subject: {safeSubject} Body: {safeBody}";
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
    public string ModelName => "phi3:mini";

    public string DownloadUrl => "https://ollama.com/download";

    public Task<OllamaSetupState> GetSetupStateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new OllamaSetupState(false, false, false, null, Array.Empty<string>(), DownloadUrl));

    public Task<string?> SuggestFolderNameAsync(string subject, string bodyText, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
