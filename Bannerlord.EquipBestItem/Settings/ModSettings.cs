using System;

namespace Bannerlord.EquipBestItem.Settings;

/// <summary>Mod configuration, stored as settings.json in the mod config directory.</summary>
public sealed class ModSettings
{
    public AiSettings Ai { get; set; } = new();
}

/// <summary>
///     LLM connection used by the natural-language request interpreter.
///     The API key is read from an environment variable by default so that the
///     config file can be shared safely; an inline key overrides it if set.
/// </summary>
public sealed class AiSettings
{
    public string Provider { get; set; } = "anthropic";

    /// <summary>Empty = the provider's default endpoint.</summary>
    public string Endpoint { get; set; } = "";

    public string Model { get; set; } = "claude-haiku-4-5";

    public string ApiKeyEnvironmentVariable { get; set; } = "EBI_AI_API_KEY";

    public string ApiKey { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxTokens { get; set; } = 1024;

    public string ResolveApiKey()
    {
        if (!string.IsNullOrEmpty(ApiKey)) return ApiKey;

        return string.IsNullOrEmpty(ApiKeyEnvironmentVariable)
            ? ""
            : Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable) ?? "";
    }

    public bool IsConfigured => ResolveApiKey().Length > 0;
}
