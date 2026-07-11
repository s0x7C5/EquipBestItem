using System;
using Newtonsoft.Json;

namespace Bannerlord.EquipBestItem.Settings;

/// <summary>Mod configuration, stored as settings.json in the mod config directory.</summary>
public sealed class ModSettings
{
    /// <summary>
    ///     "weights" — score items by the per-slot parameter weights (the mod's
    ///     main mode). "effectiveness" — use the game's built-in aggregate score.
    /// </summary>
    public string SearchMethod { get; set; } = "weights";

    /// <summary>
    ///     Search candidates in the left panel (merchant, loot, stash). Off by
    ///     default so "equip best" does not silently buy out shops; toggled
    ///     from the inventory UI.
    /// </summary>
    public bool SearchLeftPanel { get; set; }

    /// <summary>Search candidates in the player inventory panel.</summary>
    public bool SearchRightPanel { get; set; } = true;

    /// <summary>
    ///     Tint of the per-slot equip buttons, "#RRGGBB" or "#RRGGBBAA".
    ///     White keeps the sprite's own color.
    /// </summary>
    public string SlotButtonColor { get; set; } = "#FFFFFF";

    public AiSettings Ai { get; set; } = new();

    public bool UseEffectiveness =>
        string.Equals(SearchMethod, "effectiveness", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
///     LLM connection used by the natural-language request interpreter.
///
///     Zero-config path: leave everything empty — a local OpenAI-compatible
///     backend (Ollama, LM Studio, Player2) is auto-detected at startup and
///     needs no key. Explicit endpoints: any OpenAI-compatible server
///     (Ollama "http://localhost:11434/v1/chat/completions", OpenRouter
///     "https://openrouter.ai/api/v1/chat/completions", ...) with provider
///     "openai", or the Anthropic API with provider "anthropic". Cloud
///     providers read the key from the environment variable by default so the
///     config file stays safe to share.
/// </summary>
public sealed class AiSettings
{
    /// <summary>"openai" (any OpenAI-compatible endpoint) or "anthropic".</summary>
    public string Provider { get; set; } = "openai";

    /// <summary>Empty = auto-detected local backend, or the provider's default endpoint.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Empty = the auto-detected backend's model, or the provider default.</summary>
    public string Model { get; set; } = "";

    public string ApiKeyEnvironmentVariable { get; set; } = "EBI_AI_API_KEY";

    public string ApiKey { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    ///     Ask OpenAI-compatible backends for a guaranteed-JSON response
    ///     (response_format json_object). Disable if a backend rejects it.
    /// </summary>
    public bool UseJsonResponseFormat { get; set; } = true;

    /// <summary>Filled by the startup probe; never persisted.</summary>
    [JsonIgnore]
    public string? AutoDetectedEndpoint { get; internal set; }

    [JsonIgnore]
    public string? AutoDetectedModel { get; internal set; }

    /// <summary>True when nothing is configured and a local backend answered the probe.</summary>
    [JsonIgnore]
    public bool UsesAutoDetectedBackend =>
        AutoDetectedEndpoint is not null && Endpoint.Length == 0 && ResolveApiKey().Length == 0;

    [JsonIgnore]
    public bool IsLocalEndpoint =>
        Endpoint.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0 ||
        Endpoint.Contains("127.0.0.1");

    public string ResolveApiKey()
    {
        if (!string.IsNullOrEmpty(ApiKey)) return ApiKey;

        return string.IsNullOrEmpty(ApiKeyEnvironmentVariable)
            ? ""
            : Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable) ?? "";
    }

    /// <summary>Local endpoints and auto-detected backends work without a key.</summary>
    public bool IsConfigured =>
        ResolveApiKey().Length > 0 || (Endpoint.Length > 0 && IsLocalEndpoint) || UsesAutoDetectedBackend;
}
