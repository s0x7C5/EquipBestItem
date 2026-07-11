using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Bannerlord.EquipBestItem.Settings;
using Newtonsoft.Json.Linq;
using TaleWorlds.Localization;

namespace Bannerlord.EquipBestItem.Ai;

/// <summary>
///     The MCM "connection test": verifies the configured endpoint (plus the
///     API key, when one is set) by listing the server's models, and fills the
///     model setting in when the player left it empty. No automatic discovery —
///     the endpoint is always explicit.
/// </summary>
public static class BackendConnectionTest
{
    /// <summary>Runs the test and returns a localized, human-readable verdict.</summary>
    public static async Task<string> TestAsync(AiSettings settings)
    {
        if (string.Equals(settings.Provider, "anthropic", StringComparison.OrdinalIgnoreCase) &&
            settings.Endpoint.Length == 0)
            return new TextObject(
                "{=EbiAiAnthropicNote}Anthropic API is configured; the key is verified on the first request.").ToString();

        var apiKey = settings.ResolveApiKey();
        var endpoint = settings.Endpoint.Length > 0
            ? settings.Endpoint
            : apiKey.Length > 0 ? "https://api.openai.com/v1/chat/completions" : null;

        if (endpoint is null)
            return new TextObject(
                "{=EbiAiNoEndpoint}Endpoint is empty. Enter the server address (e.g. http://localhost:1234) and run the test again.").ToString();

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            if (apiKey.Length > 0)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var body = await client.GetStringAsync(DeriveModelsUrl(endpoint)).ConfigureAwait(false);
            var firstModel = FirstChatModel(body);

            // Fill the model in automatically when the player left it empty.
            if (settings.Model.Length == 0 && !string.IsNullOrEmpty(firstModel))
                settings.Model = firstModel!;

            return new TextObject("{=EbiAiConnectionOk}Connection OK: {ENDPOINT} (model: {MODEL}).")
                .SetTextVariable("ENDPOINT", endpoint)
                .SetTextVariable("MODEL", settings.Model.Length > 0 ? settings.Model : firstModel ?? "?")
                .ToString();
        }
        catch (Exception exception)
        {
            return new TextObject("{=EbiAiConnectionFail}Connection failed: {ERROR}")
                .SetTextVariable("ERROR", exception.Message).ToString();
        }
    }

    /// <summary>
    ///     First usable chat model from a /v1/models response — embedding
    ///     models sometimes come first in the list and cannot chat.
    /// </summary>
    private static string? FirstChatModel(string body)
    {
        var data = JObject.Parse(body)["data"];
        if (data is null) return null;

        foreach (var entry in data)
        {
            var id = entry["id"]?.ToString();
            if (string.IsNullOrEmpty(id)) continue;
            if (id!.IndexOf("embed", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            return id;
        }

        return null;
    }

    /// <summary>GET-able models list URL next to a chat completions endpoint.</summary>
    private static string DeriveModelsUrl(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed.Substring(0, trimmed.Length - "/chat/completions".Length) + "/models"
            : trimmed + "/v1/models";
    }
}
