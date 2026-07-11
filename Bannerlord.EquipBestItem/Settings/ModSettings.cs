using System;

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
///     The endpoint is any OpenAI-compatible server — local
///     ("http://localhost:1234", the chat-completions path is appended
///     automatically) or cloud (OpenRouter etc.) — with provider "openai",
///     or the Anthropic API with provider "anthropic". The MCM "connection
///     test" verifies the endpoint and fills in the model. Cloud providers
///     read the key from the environment variable by default so the config
///     file stays safe to share; local servers need no key.
/// </summary>
public sealed class AiSettings
{
    /// <summary>"openai" (any OpenAI-compatible endpoint) or "anthropic".</summary>
    public string Provider { get; set; } = "openai";

    /// <summary>Empty = the provider's default endpoint (cloud, needs a key).</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Empty = filled by the connection test, or the provider default.</summary>
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

    public string ResolveApiKey()
    {
        if (!string.IsNullOrEmpty(ApiKey)) return ApiKey;

        return string.IsNullOrEmpty(ApiKeyEnvironmentVariable)
            ? ""
            : Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable) ?? "";
    }

    /// <summary>
    ///     An explicit endpoint (local or LAN) works without a key — servers
    ///     that need one reject the request themselves with a clear error.
    /// </summary>
    public bool IsConfigured =>
        ResolveApiKey().Length > 0 || Endpoint.Length > 0;
}
