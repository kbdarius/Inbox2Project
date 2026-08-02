using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace Inbox2Project.Services;

public sealed class OpenAiBillingService
{
    private const string CostsUrl = "https://api.openai.com/v1/organization/costs";
    private const decimal InitialBalance = 4.82m;

    private readonly string _statePath;
    private readonly HttpClient _httpClient;

    public OpenAiBillingService(HttpClient? httpClient = null, string? appDataPath = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var appData = appDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appRoot = Path.Combine(appData, "Inbox2Project");
        Directory.CreateDirectory(appRoot);
        _statePath = Path.Combine(appRoot, "openai-billing.json");
    }

    public OpenAiBillingState LoadState()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return new OpenAiBillingState(InitialBalance, null, 0m, null, false);
            }

            var json = File.ReadAllText(_statePath);
            var stored = JsonSerializer.Deserialize<StoredBillingState>(json);
            return stored is null
                ? new OpenAiBillingState(InitialBalance, null, 0m, null, false)
                : new OpenAiBillingState(
                    stored.StartingBalance,
                    stored.BaselineUtc,
                    stored.SpentSinceBaseline,
                    stored.LastRefreshedUtc,
                    !string.IsNullOrWhiteSpace(stored.EncryptedAdminKey));
        }
        catch
        {
            return new OpenAiBillingState(InitialBalance, null, 0m, null, false);
        }
    }

    public void SaveAdminApiKey(string adminApiKey)
    {
        var normalized = adminApiKey?.Trim() ?? string.Empty;
        if (!normalized.StartsWith("sk-admin-", StringComparison.Ordinal) || normalized.Length < 20)
        {
            throw new ArgumentException("Enter a valid OpenAI Admin API key beginning with 'sk-admin-'.", nameof(adminApiKey));
        }

        var state = ReadStoredState();
        state.EncryptedAdminKey = Protect(normalized);
        WriteStoredState(state);
    }

    public void SetStartingBalance(decimal startingBalance)
    {
        if (startingBalance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startingBalance), "The starting balance cannot be negative.");
        }

        var state = ReadStoredState();
        state.StartingBalance = decimal.Round(startingBalance, 2);
        state.BaselineUtc = DateTimeOffset.UtcNow;
        state.SpentSinceBaseline = 0m;
        state.LastRefreshedUtc = null;
        WriteStoredState(state);
    }

    public async Task<OpenAiBillingRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var state = ReadStoredState();
        if (string.IsNullOrWhiteSpace(state.EncryptedAdminKey))
        {
            return OpenAiBillingRefreshResult.Failure("Enter and save the Admin API key first.");
        }

        var adminKey = Unprotect(state.EncryptedAdminKey);
        var baselineWasInitialized = false;
        if (state.BaselineUtc is null)
        {
            state.BaselineUtc = DateTimeOffset.UtcNow;
            WriteStoredState(state);
            baselineWasInitialized = true;
        }

        var startTime = state.BaselineUtc.Value.ToUnixTimeSeconds();
        var url = $"{CostsUrl}?start_time={startTime}&limit=180";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = (int)response.StatusCode switch
                {
                    401 or 403 => "Admin API key rejected or missing billing permission.",
                    429 => "OpenAI billing request was rate limited.",
                    _ => $"OpenAI billing returned HTTP {(int)response.StatusCode}.",
                };
                return OpenAiBillingRefreshResult.Failure(message);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var spent = ReadUsdSpend(document.RootElement);
            state.SpentSinceBaseline = spent;
            state.LastRefreshedUtc = DateTimeOffset.UtcNow;
            WriteStoredState(state);

            var remaining = Math.Max(0m, state.StartingBalance - spent);
            return new OpenAiBillingRefreshResult(
                true,
                state.StartingBalance,
                spent,
                remaining,
                state.BaselineUtc.Value,
                baselineWasInitialized
                    ? $"Baseline initialized and updated {state.LastRefreshedUtc.Value.ToLocalTime():g}"
                    : $"Updated {state.LastRefreshedUtc.Value.ToLocalTime():g}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OpenAiBillingRefreshResult.Failure("Billing request timed out.");
        }
        catch (HttpRequestException)
        {
            return OpenAiBillingRefreshResult.Failure("Could not reach OpenAI billing.");
        }
        catch (JsonException)
        {
            return OpenAiBillingRefreshResult.Failure("OpenAI returned an unreadable billing response.");
        }
    }

    private StoredBillingState ReadStoredState()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                return JsonSerializer.Deserialize<StoredBillingState>(File.ReadAllText(_statePath))
                    ?? new StoredBillingState { StartingBalance = InitialBalance };
            }
        }
        catch
        {
            // Recreate a safe default below if the local state is unreadable.
        }

        return new StoredBillingState { StartingBalance = InitialBalance };
    }

    private void WriteStoredState(StoredBillingState state)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_statePath, json);
    }

    private static decimal ReadUsdSpend(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
        {
            return 0m;
        }

        var total = 0m;
        foreach (var bucket in buckets.EnumerateArray())
        {
            if (!bucket.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var result in results.EnumerateArray())
            {
                if (!result.TryGetProperty("amount", out var amount)
                    || !amount.TryGetProperty("currency", out var currency)
                    || !string.Equals(currency.GetString(), "usd", StringComparison.OrdinalIgnoreCase)
                    || !amount.TryGetProperty("value", out var value)
                    || !value.TryGetDecimal(out var decimalValue))
                {
                    continue;
                }

                total += decimalValue;
            }
        }

        return decimal.Round(total, 4);
    }

    [SupportedOSPlatform("windows")]
    private static string Protect(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    [SupportedOSPlatform("windows")]
    private static string Unprotect(string value)
    {
        var protectedBytes = Convert.FromBase64String(value);
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser));
    }

    private sealed class StoredBillingState
    {
        public string? EncryptedAdminKey { get; set; }
        public decimal StartingBalance { get; set; } = InitialBalance;
        public DateTimeOffset? BaselineUtc { get; set; }
        public decimal SpentSinceBaseline { get; set; }
        public DateTimeOffset? LastRefreshedUtc { get; set; }
    }
}

public sealed record OpenAiBillingState(
    decimal StartingBalance,
    DateTimeOffset? BaselineUtc,
    decimal SpentSinceBaseline,
    DateTimeOffset? LastRefreshedUtc,
    bool IsAdminApiKeyConfigured);

public sealed record OpenAiBillingRefreshResult(
    bool IsSuccess,
    decimal StartingBalance,
    decimal SpentSinceBaseline,
    decimal EstimatedRemaining,
    DateTimeOffset? BaselineUtc,
    string Message)
{
    public static OpenAiBillingRefreshResult Failure(string message) =>
        new(false, 0m, 0m, 0m, null, message);
}
