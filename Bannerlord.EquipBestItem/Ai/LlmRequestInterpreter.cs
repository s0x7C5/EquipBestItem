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

    // Quirks learned from the backend's responses, remembered for the session
    // so every request after the first goes straight to the shape that works
    // (one HTTP call instead of up to three).
    private bool _endpointRejectsResponseFormat;
    private bool _systemRoleDropped;

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

        var systemPrompt = BuildSystemPrompt(context);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        var includeResponseFormat = !_endpointRejectsResponseFormat;
        var mergeSystemIntoUser = _systemRoleDropped;
        var (statusCode, body) = await SendAsync(
            isAnthropic
                ? BuildAnthropicRequest(request, systemPrompt, apiKey)
                : BuildOpenAiRequest(request, systemPrompt, apiKey, includeResponseFormat, mergeSystemIntoUser),
            timeout.Token).ConfigureAwait(false);

        // Some OpenAI-compatible servers (LM Studio) reject response_format
        // "json_object"; retry once without it instead of failing the request.
        if (statusCode == 400 && !isAnthropic && _settings.UseJsonResponseFormat && includeResponseFormat &&
            body.IndexOf("response_format", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            _endpointRejectsResponseFormat = true;
            includeResponseFormat = false;
            (statusCode, body) = await SendAsync(
                BuildOpenAiRequest(request, systemPrompt, apiKey, includeResponseFormat, mergeSystemIntoUser),
                timeout.Token).ConfigureAwait(false);
        }

        // Some chat templates silently discard the system role (LM Studio
        // community GGUFs); the reported prompt size then cannot possibly
        // contain our instructions. Retry with everything in the user message.
        if (statusCode is >= 200 and < 300 && !isAnthropic && !mergeSystemIntoUser &&
            SystemPromptWasDropped(body, systemPrompt))
        {
            _systemRoleDropped = true;
            (statusCode, body) = await SendAsync(
                BuildOpenAiRequest(request, systemPrompt, apiKey, includeResponseFormat, mergeSystemIntoUser: true),
                timeout.Token).ConfigureAwait(false);
        }

        if (statusCode is < 200 or >= 300)
            throw new InvalidOperationException($"AI request failed ({statusCode}): {Truncate(body)}");

        var text = isAnthropic ? ReadAnthropicText(body) : ReadOpenAiText(body);
        return LlmPlanParser.Parse(text);
    }

    private HttpRequestMessage BuildAnthropicRequest(string request, string systemPrompt, string apiKey)
    {
        var endpoint = _settings.Endpoint.Length > 0 ? _settings.Endpoint : AnthropicDefaultEndpoint;
        var model = _settings.Model.Length > 0 ? _settings.Model : AnthropicDefaultModel;

        var payload = new JObject
        {
            ["model"] = model,
            ["max_tokens"] = _settings.MaxTokens,
            ["temperature"] = 0,
            ["system"] = systemPrompt,
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

    private static async Task<(int StatusCode, string Body)> SendAsync(
        HttpRequestMessage httpRequest, CancellationToken cancellationToken)
    {
        using (httpRequest)
        using (var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
        {
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ((int)response.StatusCode, body);
        }
    }

    /// <summary>
    ///     True when usage.prompt_tokens is too small to contain the system
    ///     prompt. Even the densest tokenizer packs no more than ~8 characters
    ///     into one token, so a smaller count means the role was dropped.
    /// </summary>
    private static bool SystemPromptWasDropped(string body, string systemPrompt)
    {
        try
        {
            var promptTokens = JObject.Parse(body)["usage"]?["prompt_tokens"]?.Value<int?>();
            return promptTokens is int tokens && tokens > 0 && tokens < systemPrompt.Length / 8;
        }
        catch
        {
            return false;
        }
    }

    private HttpRequestMessage BuildOpenAiRequest(
        string request, string systemPrompt, string apiKey,
        bool includeResponseFormat, bool mergeSystemIntoUser = false)
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

        var messages = mergeSystemIntoUser
            ? new JArray(new JObject
            {
                ["role"] = "user",
                ["content"] = $"{systemPrompt}\nPlayer request: \"{request}\""
            })
            : new JArray(
                new JObject { ["role"] = "system", ["content"] = systemPrompt },
                new JObject { ["role"] = "user", ["content"] = request });

        var payload = new JObject
        {
            ["model"] = model,
            ["max_tokens"] = _settings.MaxTokens,
            ["temperature"] = 0,
            ["messages"] = messages
        };

        if (_settings.UseJsonResponseFormat && includeResponseFormat)
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
  "explanation": "one short sentence describing what you set up, written in the same language as the player request",
  "directives": [
    {
      "slot": "Head|Cape|Body|Gloves|Leg|Horse|HorseHarness|Weapon0|Weapon1|Weapon2|Weapon3|AllArmor|AllWeapons|AllMount|All",
      "weights": { "<param>": <float -1..1> },
      "maxItemWeight": <optional float, kg>,
      "culture": "<optional: empire|sturgia|aserai|vlandia|battania|khuzait>",
      "weaponClass": "<optional, one of: OneHandedSword, TwoHandedSword, OneHandedAxe, TwoHandedAxe, Mace, TwoHandedMace, Dagger, OneHandedPolearm, TwoHandedPolearm, Bow, Crossbow, Arrow, Bolt, Javelin, ThrowingAxe, ThrowingKnife, SmallShield, LargeShield>"
    }
  ]
}
""");
        builder.AppendLine();
        builder.AppendLine(
            "Scoring params: HeadArmor, BodyArmor, ArmArmor, LegArmor, MountArmor, ChargeDamage, HitPoints, " +
            "Maneuver, Speed, MaxAmmo, ThrustSpeed, SwingSpeed, MissileSpeed, MissileDamage, WeaponLength, " +
            "ThrustDamage, SwingDamage, Accuracy, Handling, Weight (the item's physical mass in kg — " +
            "heaviness, NOT protection).");
        builder.AppendLine(
            "Positive weight = maximize, negative = minimize (e.g. Weight: -1 prefers light items). " +
            "Only spell out weights when the player expressed a preference; an empty weights object " +
            "means \"just find the best with default balanced weights\".");
        builder.AppendLine(
            "\"Protection\"/\"armor\"/\"защита\"/\"броня\" maps to the matching *Armor param for that body " +
            "area: head->HeadArmor, body/torso->BodyArmor, arms/hands->ArmArmor, legs/feet->LegArmor, " +
            "mount->MountArmor. Never use Weight to express protection.");
        builder.AppendLine(
            "Use one directive per distinct intent. Prefer group slots (AllArmor) when the request is broad. " +
            "weaponClass only makes sense for weapon slots. Match the grip exactly: " +
            "one-handed/одноручный -> OneHanded…, two-handed/двуручный -> TwoHanded…, never swap them; " +
            "when the player does not say which, pick the OneHanded… variant.");
        builder.AppendLine(
            "When the request names one specific piece of gear (helmet/шлем, gloves/перчатки, boots or " +
            "greaves/поножи, cape/плащ, cuirass/кираса), emit exactly one directive for that piece's slot " +
            "and do not invent directives for slots the player did not mention. If the request also names a " +
            "body area that slot cannot cover, keep the named slot and apply the protection to what it covers.");
        builder.AppendLine();
        builder.AppendLine("Examples:");
        builder.AppendLine("""User: "одень меня в самую лёгкую броню империи" -> {"explanation":"Ищу самую лёгкую имперскую броню.","directives":[{"slot":"AllArmor","weights":{"Weight":-1.0},"culture":"empire"}]}""");
        builder.AppendLine("""User: "give me a better bow and plenty of arrows" -> {"explanation":"Better bow, arrows with the biggest stack.","directives":[{"slot":"Weapon0","weights":{},"weaponClass":"Bow"},{"slot":"Weapon1","weights":{"MaxAmmo":1.0},"weaponClass":"Arrow"}]}""");
        builder.AppendLine("""User: "найди шлем с лучшей защитой ног" -> {"explanation":"Беру шлем с максимальной защитой (защита ног шлему недоступна).","directives":[{"slot":"Head","weights":{"HeadArmor":1.0}}]}""");
        builder.AppendLine("""User: "в первый слот оружия поставь одноручный топор" -> {"explanation":"Ищу одноручный топор для первого слота оружия.","directives":[{"slot":"Weapon0","weights":{},"weaponClass":"OneHandedAxe"}]}""");
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
