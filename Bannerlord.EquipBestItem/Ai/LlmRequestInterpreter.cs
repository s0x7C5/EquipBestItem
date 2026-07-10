using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bannerlord.EquipBestItem.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bannerlord.EquipBestItem.Ai;

/// <summary>
///     Interprets player requests with an LLM over HTTP. The primary path is
///     any OpenAI-compatible endpoint (auto-detected local Ollama / LM Studio /
///     Player2, or an explicit one like OpenRouter); the Anthropic Messages API
///     is available via provider "anthropic". Requests are tuned for small
///     local models: temperature 0, JSON response format and few-shot examples.
/// </summary>
public sealed class LlmRequestInterpreter : IRequestInterpreter
{
    private const string AnthropicDefaultEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicDefaultModel = "claude-haiku-4-5";
    private const string OpenAiDefaultEndpoint = "https://api.openai.com/v1/chat/completions";
    private const string OpenAiDefaultModel = "gpt-4o-mini";

    private static readonly HttpClient HttpClient = new();

    private readonly AiSettings _settings;

    public LlmRequestInterpreter(AiSettings settings)
    {
        _settings = settings;
    }

    public async Task<InterpretedPlan> InterpretAsync(
        string request, InterpretationContext context, CancellationToken cancellationToken)
    {
        var apiKey = _settings.ResolveApiKey();
        var isAnthropic =
            string.Equals(_settings.Provider, "anthropic", StringComparison.OrdinalIgnoreCase) &&
            !_settings.UsesAutoDetectedBackend;

        // Local backends (explicit or auto-detected) work without a key.
        var isKeyless = _settings.UsesAutoDetectedBackend || _settings.IsLocalEndpoint;
        if (apiKey.Length == 0 && !isKeyless)
            throw new InvalidOperationException(
                $"No AI API key. Set the {_settings.ApiKeyEnvironmentVariable} environment variable, " +
                "the ai.apiKey value in settings.json, or run a local backend (Ollama / LM Studio / Player2).");

        using var httpRequest = isAnthropic
            ? BuildAnthropicRequest(request, context, apiKey)
            : BuildOpenAiRequest(request, context, apiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        using var response = await HttpClient.SendAsync(httpRequest, timeout.Token).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"AI request failed ({(int)response.StatusCode}): {Truncate(body)}");

        var text = isAnthropic ? ReadAnthropicText(body) : ReadOpenAiText(body);
        return LlmPlanParser.Parse(text);
    }

    private HttpRequestMessage BuildAnthropicRequest(string request, InterpretationContext context, string apiKey)
    {
        var endpoint = _settings.Endpoint.Length > 0 ? _settings.Endpoint : AnthropicDefaultEndpoint;
        var model = _settings.Model.Length > 0 ? _settings.Model : AnthropicDefaultModel;

        var payload = new JObject
        {
            ["model"] = model,
            ["max_tokens"] = _settings.MaxTokens,
            ["temperature"] = 0,
            ["system"] = BuildSystemPrompt(context),
            ["messages"] = new JArray(new JObject
            {
                ["role"] = "user",
                ["content"] = request
            })
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        return httpRequest;
    }

    private HttpRequestMessage BuildOpenAiRequest(string request, InterpretationContext context, string apiKey)
    {
        string endpoint;
        string model;

        if (_settings.UsesAutoDetectedBackend)
        {
            endpoint = _settings.AutoDetectedEndpoint!;
            model = _settings.AutoDetectedModel!;
        }
        else
        {
            endpoint = _settings.Endpoint.Length > 0 ? _settings.Endpoint : OpenAiDefaultEndpoint;
            model = _settings.Model.Length > 0 ? _settings.Model : OpenAiDefaultModel;
        }

        var payload = new JObject
        {
            ["model"] = model,
            ["max_tokens"] = _settings.MaxTokens,
            ["temperature"] = 0,
            ["messages"] = new JArray(
                new JObject { ["role"] = "system", ["content"] = BuildSystemPrompt(context) },
                new JObject { ["role"] = "user", ["content"] = request })
        };

        if (_settings.UseJsonResponseFormat)
            payload["response_format"] = new JObject { ["type"] = "json_object" };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json")
        };

        if (apiKey.Length > 0)
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

        return httpRequest;
    }

    private static string ReadAnthropicText(string body) =>
        JObject.Parse(body)["content"]?[0]?["text"]?.ToString()
        ?? throw new FormatException("Unexpected Anthropic response shape.");

    private static string ReadOpenAiText(string body) =>
        JObject.Parse(body)["choices"]?[0]?["message"]?["content"]?.ToString()
        ?? throw new FormatException("Unexpected chat completions response shape.");

    private static string BuildSystemPrompt(InterpretationContext context)
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            "You translate a Mount & Blade II: Bannerlord player's natural-language equipment request " +
            "into a JSON search plan. Respond with a single JSON object and nothing else.");
        builder.AppendLine();
        builder.AppendLine("JSON shape:");
        builder.AppendLine("""
{
  "explanation": "one short sentence in the player's language describing what you set up",
  "directives": [
    {
      "slot": "Head|Cape|Body|Gloves|Leg|Horse|HorseHarness|Weapon0|Weapon1|Weapon2|Weapon3|AllArmor|AllWeapons|AllMount|All",
      "weights": { "<param>": <float -1..1> },
      "maxItemWeight": <optional float, kg>,
      "culture": "<optional: empire|sturgia|aserai|vlandia|battania|khuzait>",
      "weaponClass": "<optional TaleWorlds WeaponClass, e.g. OneHandedSword, TwoHandedAxe, Bow, Crossbow, Arrow, Bolt, Javelin, ThrowingAxe, ThrowingKnife, SmallShield, LargeShield, OneHandedPolearm, TwoHandedPolearm, Mace, TwoHandedMace, Dagger>"
    }
  ]
}
""");
        builder.AppendLine();
        builder.AppendLine(
            "Weight params: HeadArmor, BodyArmor, ArmArmor, LegArmor, MountArmor, ChargeDamage, HitPoints, " +
            "Maneuver, Speed, MaxAmmo, ThrustSpeed, SwingSpeed, MissileSpeed, MissileDamage, WeaponLength, " +
            "ThrustDamage, SwingDamage, Accuracy, Handling, Weight.");
        builder.AppendLine(
            "Positive weight = maximize, negative = minimize (e.g. Weight: -1 prefers light items). " +
            "Only spell out weights when the player expressed a preference; an empty weights object " +
            "means \"just find the best with default balanced weights\".");
        builder.AppendLine(
            "Use one directive per distinct intent. Prefer group slots (AllArmor) when the request is broad. " +
            "weaponClass only makes sense for weapon slots.");
        builder.AppendLine();
        builder.AppendLine("Examples:");
        builder.AppendLine("""User: "одень меня в самую лёгкую броню империи" -> {"explanation":"Ищу самую лёгкую имперскую броню.","directives":[{"slot":"AllArmor","weights":{"Weight":-1.0},"culture":"empire"}]}""");
        builder.AppendLine("""User: "give me a better bow and plenty of arrows" -> {"explanation":"Better bow, arrows with the biggest stack.","directives":[{"slot":"Weapon0","weights":{},"weaponClass":"Bow"},{"slot":"Weapon1","weights":{"MaxAmmo":1.0},"weaponClass":"Arrow"}]}""");
        builder.AppendLine("""User: "просто одень получше" -> {"explanation":"Подбираю лучшее по всем слотам.","directives":[{"slot":"All","weights":{}}]}""");
        builder.AppendLine();
        builder.AppendLine($"Current character: {context.CharacterName}.");
        builder.AppendLine($"Active equipment set: {context.EquipmentSetKey}.");

        if (context.NotableSkills.Count > 0)
            builder.AppendLine($"Notable skills: {string.Join(", ", context.NotableSkills)}.");

        return builder.ToString();
    }

    private static string Truncate(string text) =>
        text.Length <= 300 ? text : text.Substring(0, 300) + "…";
}
