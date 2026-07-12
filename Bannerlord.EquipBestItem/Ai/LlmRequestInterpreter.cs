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
        var isAnthropic = string.Equals(_settings.Provider, "anthropic", StringComparison.OrdinalIgnoreCase);

        // Explicit endpoints (local or LAN) work without a key; servers that
        // require one reject the request themselves with a clear error.
        if (apiKey.Length == 0 && _settings.Endpoint.Length == 0)
            throw new InvalidOperationException(
                "No AI backend configured. Set the endpoint (e.g. http://localhost:1234 for a local " +
                $"server) in MCM or settings.json, or provide an API key via {_settings.ApiKeyEnvironmentVariable}.");

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
        var endpoint = _settings.Endpoint.Length > 0 ? NormalizeEndpoint(_settings.Endpoint) : OpenAiDefaultEndpoint;
        var model = _settings.Model.Length > 0 ? _settings.Model : OpenAiDefaultModel;

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

    /// <summary>
    ///     A bare server address (no path) gets the standard chat-completions
    ///     path appended — the most common misconfiguration. Explicit paths
    ///     are respected as-is.
    /// </summary>
    private static string NormalizeEndpoint(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.AbsolutePath is "" or "/"
            ? trimmed + "/v1/chat/completions"
            : endpoint;
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
  "explanation": "one short sentence describing what you set up",
  "target": "<optional, whose gear the request is about: current (default) | others (every party hero except the main one) | all (every party hero) | an exact hero name from the party list>",
  "directives": [
    {
      "slot": "Head|Cape|Body|Gloves|Leg|Horse|HorseHarness|Weapon0|Weapon1|Weapon2|Weapon3|AllArmor|AllWeapons|AllMount|All",
      "weights": { "<param>": <float -1..1> },
      "maxItemWeight": <optional float, kg>,
      "culture": "<optional: empire|sturgia|aserai|vlandia|battania|khuzait>",
      "weaponClass": "<optional, one of: OneHandedSword, TwoHandedSword, OneHandedAxe, TwoHandedAxe, Mace, TwoHandedMace, Dagger, OneHandedPolearm, TwoHandedPolearm, ShortBow, LongBow, Crossbow, Arrow, Bolt, Javelin, ThrowingAxe, ThrowingKnife, SmallShield, LargeShield>",
      "priorities": ["<optional: ranked stat groups, most important first; join equal-rank stats with +, e.g. \"HeadArmor\", \"HitPoints+BodyArmor\">"]
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
            $"The player's search method is \"{context.SearchMethod}\". When it is \"priority\", express " +
            "stat preferences as \"priorities\" — an ordered list, most important first, \"A+B\" meaning " +
            "equal rank — and leave \"weights\" empty; otherwise use \"weights\" and omit \"priorities\". " +
            "Never put Weight into priorities: lightness is a maxItemWeight cap, not a rank. " +
            "When the method is \"effectiveness\", items are ranked by the game's built-in score: emit only " +
            "weaponClass/culture/maxItemWeight and leave weights and priorities out — unless the player " +
            "explicitly asks to rank by specific stats, in which case emit weights (that switches the slot " +
            "to weighted scoring).");
        builder.AppendLine(
            "Words meaning protection/armor (in any language) map to the matching *Armor param for that " +
            "body area: head->HeadArmor, body/torso->BodyArmor, arms/hands->ArmArmor, legs/feet->LegArmor, " +
            "mount->MountArmor. Never use Weight to express protection.");
        builder.AppendLine(
            "target: only set it when the player names other heroes (\"everyone\", \"everyone except me\", " +
            "a companion's name); requests about \"me\" or with no one named mean current.");
        builder.AppendLine(
            "Use one directive per distinct intent. Prefer group slots (AllArmor) when the request is broad. " +
            "weaponClass only makes sense for weapon slots. Match the grip exactly, whatever the request " +
            "language: one-handed -> OneHanded…, two-handed -> TwoHanded…, never swap them; " +
            "when the player does not say which, pick the OneHanded… variant. " +
            "Bows are two classes: LongBow cannot be fired from horseback; when the player just says " +
            "\"a bow\", pick ShortBow (usable everywhere) unless they clearly fight on foot.");
        builder.AppendLine(
            "When the request names one specific piece of gear (a helmet, gloves, boots, a cape, a cuirass — " +
            "in any language), emit exactly one directive for that piece's slot " +
            "and do not invent directives for slots the player did not mention. If the request also names a " +
            "body area that slot cannot cover, keep the named slot and apply the protection to what it covers.");

        if (context.LanguageGlossary.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine(context.LanguageGlossary);
        }

        builder.AppendLine();
        builder.AppendLine("Examples:");
        builder.AppendLine("""User: "dress me in the lightest imperial armor" -> {"explanation":"Looking for the lightest imperial armor.","directives":[{"slot":"AllArmor","weights":{"Weight":-1.0},"culture":"empire"}]}""");
        builder.AppendLine("""User: "give me a better bow and plenty of arrows" -> {"explanation":"Better bow, arrows with the biggest stack.","directives":[{"slot":"Weapon0","weights":{},"weaponClass":"ShortBow"},{"slot":"Weapon1","weights":{"MaxAmmo":1.0},"weaponClass":"Arrow"}]}""");
        builder.AppendLine("""User: "find a helmet with the best leg protection" -> {"explanation":"Picking the best-protecting helmet (a helmet cannot protect legs).","directives":[{"slot":"Head","weights":{"HeadArmor":1.0}}]}""");
        builder.AppendLine("""User: "put a one-handed axe in the first weapon slot" -> {"explanation":"Looking for a one-handed axe for the first weapon slot.","directives":[{"slot":"Weapon0","weights":{},"weaponClass":"OneHandedAxe"}]}""");
        builder.AppendLine("""User: "just give me better gear" -> {"explanation":"Picking the best items for every slot.","directives":[{"slot":"All","weights":{}}]}""");
        builder.AppendLine("""User: "set every hero except me up with a large shield in the first weapon slot" -> {"explanation":"Large shields in the first weapon slot for every other hero.","target":"others","directives":[{"slot":"Weapon0","weights":{},"weaponClass":"LargeShield"}]}""");
        builder.AppendLine("""User (search method "priority"): "shield: hit points above all, then speed and armor equally" -> {"explanation":"Shield ranked by hit points, then speed and armor as equals.","directives":[{"slot":"Weapon0","weights":{},"weaponClass":"LargeShield","priorities":["HitPoints","SwingSpeed+BodyArmor"]}]}""");
        builder.AppendLine();
        builder.AppendLine(
            $"Write the \"explanation\" value in {context.GameLanguage} (the game's language), " +
            "no matter what language the player typed the request in. Everything else stays as specified.");
        builder.AppendLine();
        builder.AppendLine($"Current character: {context.CharacterName}.");
        builder.AppendLine($"Active equipment set: {context.EquipmentSetKey}.");
        builder.AppendLine($"Search method: {context.SearchMethod}.");

        if (context.PartyHeroes.Count > 1)
            builder.AppendLine($"Party heroes: {string.Join(", ", context.PartyHeroes)}.");

        if (context.NotableSkills.Count > 0)
            builder.AppendLine($"Notable skills: {string.Join(", ", context.NotableSkills)}.");

        return builder.ToString();
    }

    private static string Truncate(string text) =>
        text.Length <= 300 ? text : text.Substring(0, 300) + "…";
}
