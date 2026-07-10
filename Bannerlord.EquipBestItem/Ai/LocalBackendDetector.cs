using System;
using System.Net.Http;
using System.Threading.Tasks;
using Bannerlord.EquipBestItem.Settings;
using Newtonsoft.Json.Linq;

namespace Bannerlord.EquipBestItem.Ai;

/// <summary>
///     Zero-config discovery of a local LLM backend. All the runtimes the
///     modding community actually uses (Ollama, LM Studio, Player2) expose the
///     standard OpenAI-compatible GET /v1/models — the first one that answers
///     on its default port wins, and its first model is used.
/// </summary>
public static class LocalBackendDetector
{
    private static readonly string[] CandidateBaseUrls =
    {
        "http://localhost:11434", // Ollama
        "http://localhost:1234",  // LM Studio
        "http://localhost:4315"   // Player2
    };

    /// <summary>Runs in the background at startup; only fills the settings when nothing is configured.</summary>
    public static async Task DetectAsync(AiSettings settings)
    {
        if (settings.Endpoint.Length > 0 || settings.ResolveApiKey().Length > 0) return;

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        foreach (var baseUrl in CandidateBaseUrls)
        {
            try
            {
                var body = await client.GetStringAsync($"{baseUrl}/v1/models").ConfigureAwait(false);
                var model = JObject.Parse(body)["data"]?.First?["id"]?.ToString();
                if (string.IsNullOrEmpty(model)) continue;

                settings.AutoDetectedEndpoint = $"{baseUrl}/v1/chat/completions";
                settings.AutoDetectedModel = model;
                return;
            }
            catch
            {
                // Nothing listening on this port — try the next runtime.
            }
        }
    }
}
