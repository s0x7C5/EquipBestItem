using System.Threading.Tasks;
using Bannerlord.EquipBestItem.Ai;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.EquipBestItem.Settings;

/// <summary>
///     MCM front-end over <see cref="ModSettings" />. Every property delegates
///     straight to the JSON-backed settings and saves them, so settings.json
///     stays the single source of truth and the mod keeps working without MCM
///     installed (the module dependency is optional; this type is only touched
///     when the MCM module is present — see SubModule).
///
///     Only plain property types are used: MCM invokes setters for those on
///     every change, which keeps the two-way sync trivial.
/// </summary>
internal sealed class McmSettings : AttributeGlobalSettings<McmSettings>
{
    private static bool _liveCreated;

    private readonly bool _isLive;
    private readonly ModSettings? _presetDefaults;

    /// <summary>
    ///     The first instance MCM creates is the registered singleton and works
    ///     on the live settings. Later instances are the ones MCM spawns to
    ///     compute its "Default" preset — they get a factory-fresh
    ///     <see cref="ModSettings" /> instead, so "revert to default" restores
    ///     real defaults rather than echoing the current values back.
    /// </summary>
    public McmSettings()
    {
        _isLive = !_liveCreated;
        _liveCreated = true;
        if (!_isLive) _presetDefaults = new ModSettings();

        ResetSlotButtonColor = () =>
        {
            S.SlotButtonColor = "#FFFFFF";
            Persist();
            OnPropertyChanged(nameof(SlotButtonColor));
        };

        TestConnection = () =>
        {
            if (!_isLive) return;

            var settings = S.Ai;
            Task.Run(async () =>
            {
                var verdict = await BackendConnectionTest.TestAsync(settings).ConfigureAwait(false);
                MainThread.Post(() =>
                {
                    // The test may have filled in the model; save and refresh the textbox.
                    Persist();
                    OnPropertyChanged(nameof(AiModel));
                    InformationManager.ShowInquiry(new InquiryData(
                        DisplayName, verdict, true, false,
                        GameTexts.FindText("str_ok").ToString(), "", null, null));
                });
            });
        };
    }

    public override string Id => "EquipBestItem";

    public override string DisplayName => "Equip Best Item";

    public override string FolderName => "EquipBestItem";

    public override string FormatType => "json2";

    private ModSettings S => _isLive ? ModRuntime.Services.Settings : _presetDefaults!;

    private void Persist()
    {
        if (_isLive) ModRuntime.Services.PersistSettings();
    }

    // settings.json SearchMethod values, in the dropdown's option order.
    private static readonly string[] SearchMethodKeys = { "weights", "priority", "effectiveness" };

    private Dropdown<string>? _searchMethod;

    private static int SearchMethodIndex(ModSettings settings) =>
        settings.UsePriority ? 1 : settings.UseEffectiveness ? 2 : 0;

    /// <summary>
    ///     MCM mutates the returned Dropdown in place instead of calling the
    ///     setter, so the selection change is observed via the dropdown's own
    ///     PropertyChanged; the setter only runs on preset resets.
    /// </summary>
    [SettingPropertyDropdown("{=EbiMcmMethod}Search method", Order = 0, RequireRestart = false,
        HintText = "{=EbiMcmMethodHint}Parameter weights: balanced choice - every weighted stat counts, one huge stat cannot outweigh the rest, and a swap is only suggested when the item is clearly better. Stat priority: strict order - the top stat decides, lower ones only break ties; reorder and link stats in the slot filter. Game Effectiveness: the game's single built-in quality score - no setup, but crude.")]
    [SettingPropertyGroup("{=EbiMcmGroupGeneral}General", GroupOrder = 0)]
    public Dropdown<string> SearchMethod
    {
        get
        {
            if (_searchMethod is null)
            {
                _searchMethod = new Dropdown<string>(new[]
                {
                    new TextObject("{=EbiMcmMethodWeights}Parameter weights").ToString(),
                    new TextObject("{=EbiMcmMethodPriority}Stat priority order").ToString(),
                    new TextObject("{=EbiMcmMethodEffectiveness}Game Effectiveness score").ToString()
                }, SearchMethodIndex(S));

                _searchMethod.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName != nameof(Dropdown<string>.SelectedIndex)) return;

                    var index = _searchMethod.SelectedIndex;
                    if (index < 0 || index >= SearchMethodKeys.Length) return;

                    S.SearchMethod = SearchMethodKeys[index];
                    Persist();
                };
            }

            return _searchMethod;
        }
        set
        {
            // Preset reset: adopt the preset's selection into the live dropdown
            // (its PropertyChanged handler persists the mapped value).
            if (value is not null && !ReferenceEquals(value, _searchMethod))
                SearchMethod.SelectedIndex = value.SelectedIndex;
        }
    }

    [SettingPropertyInteger("{=EbiMcmUpgradeMargin}Upgrade margin, %", 0, 10, Order = 1, RequireRestart = false,
        HintText = "{=EbiMcmUpgradeMarginHint}Weights method only: a trade-off candidate (better overall but worse in some stat) must beat the equipped item's score by this margin before a swap is suggested. Items not worse in any weighted stat are suggested regardless. 0 suggests on any score edge; higher values mean calmer, more confident suggestions.")]
    [SettingPropertyGroup("{=EbiMcmGroupGeneral}General", GroupOrder = 0)]
    public int UpgradeMarginPercent
    {
        get => S.UpgradeMarginPercent;
        set { S.UpgradeMarginPercent = value; Persist(); }
    }

    [SettingPropertyText("{=EbiMcmButtonColor}Slot button color", Order = 2, RequireRestart = false,
        HintText = "{=EbiMcmButtonColorHint}Tint of the per-slot equip buttons, #RRGGBB or #RRGGBBAA. #FFFFFF keeps the original look. Applied when the inventory is reopened.")]
    [SettingPropertyGroup("{=EbiMcmGroupGeneral}General", GroupOrder = 0)]
    public string SlotButtonColor
    {
        get => S.SlotButtonColor;
        set { S.SlotButtonColor = value; Persist(); }
    }

    [SettingPropertyButton("{=EbiMcmButtonColorReset}Reset button color", Content = "{=ebi_default}Default",
        Order = 3, RequireRestart = false,
        HintText = "{=EbiMcmButtonColorHint}Tint of the per-slot equip buttons, #RRGGBB or #RRGGBBAA. #FFFFFF keeps the original look. Applied when the inventory is reopened.")]
    [SettingPropertyGroup("{=EbiMcmGroupGeneral}General", GroupOrder = 0)]
    public System.Action ResetSlotButtonColor { get; set; }

    [SettingPropertyBool("{=EbiMcmLeftPanel}Search the left panel", Order = 4, RequireRestart = false,
        HintText = "{=EbiMcmLeftPanelHint}Search the merchant/loot side too; equipping a found item buys it. Also toggled by the lock on the left plaque in the inventory.")]
    [SettingPropertyGroup("{=EbiMcmGroupGeneral}General", GroupOrder = 0)]
    public bool SearchLeftPanel
    {
        get => S.SearchLeftPanel;
        set { S.SearchLeftPanel = value; Persist(); }
    }

    [SettingPropertyBool("{=EbiMcmRightPanel}Search the inventory panel", Order = 5, RequireRestart = false,
        HintText = "{=EbiMcmRightPanelHint}Search your own inventory. Also toggled by the lock on the right plaque.")]
    [SettingPropertyGroup("{=EbiMcmGroupGeneral}General", GroupOrder = 0)]
    public bool SearchRightPanel
    {
        get => S.SearchRightPanel;
        set { S.SearchRightPanel = value; Persist(); }
    }

    [SettingPropertyBool("{=EbiMcmAnthropic}Use the Anthropic API", Order = 0, RequireRestart = false,
        HintText = "{=EbiMcmAnthropicHint}On: Anthropic Messages API. Off: any OpenAI-compatible endpoint (LM Studio, Ollama, OpenRouter, ...).")]
    [SettingPropertyGroup("{=EbiMcmGroupAi}AI assistant", GroupOrder = 1)]
    public bool AiUseAnthropic
    {
        get => string.Equals(S.Ai.Provider, "anthropic", System.StringComparison.OrdinalIgnoreCase);
        set { S.Ai.Provider = value ? "anthropic" : "openai"; Persist(); }
    }

    [SettingPropertyText("{=EbiMcmEndpoint}Endpoint", Order = 1, RequireRestart = false,
        HintText = "{=EbiMcmEndpointHint}Chat completions URL, e.g. http://localhost:1234/v1/chat/completions (a bare server address also works).")]
    [SettingPropertyGroup("{=EbiMcmGroupAi}AI assistant", GroupOrder = 1)]
    public string AiEndpoint
    {
        get => S.Ai.Endpoint;
        set { S.Ai.Endpoint = value; Persist(); }
    }

    [SettingPropertyText("{=EbiMcmModel}Model", Order = 2, RequireRestart = false,
        HintText = "{=EbiMcmModelHint}Model id. Empty = the auto-detected backend's first model, or the provider default.")]
    [SettingPropertyGroup("{=EbiMcmGroupAi}AI assistant", GroupOrder = 1)]
    public string AiModel
    {
        get => S.Ai.Model;
        set { S.Ai.Model = value; Persist(); }
    }

    [SettingPropertyText("{=EbiMcmApiKey}API key", Order = 3, RequireRestart = false,
        HintText = "{=EbiMcmApiKeyHint}Cloud providers only; local backends need none. The EBI_AI_API_KEY environment variable is used when this is empty.")]
    [SettingPropertyGroup("{=EbiMcmGroupAi}AI assistant", GroupOrder = 1)]
    public string AiApiKey
    {
        get => S.Ai.ApiKey;
        set { S.Ai.ApiKey = value; Persist(); }
    }

    [SettingPropertyInteger("{=EbiMcmTimeout}Timeout, seconds", 5, 180, Order = 4, RequireRestart = false,
        HintText = "{=EbiMcmTimeoutHint}How long to wait for the model. Slow local models may need more than the default 30.")]
    [SettingPropertyGroup("{=EbiMcmGroupAi}AI assistant", GroupOrder = 1)]
    public int AiTimeoutSeconds
    {
        get => S.Ai.TimeoutSeconds;
        set { S.Ai.TimeoutSeconds = value; Persist(); }
    }

    [SettingPropertyButton("{=EbiMcmTest}Connection test", Content = "{=EbiMcmTestButton}Run",
        Order = 5, RequireRestart = false,
        HintText = "{=EbiMcmTestHint}Requests the server's model list to verify the endpoint (and API key, if set). Fills the model field in when it is empty.")]
    [SettingPropertyGroup("{=EbiMcmGroupAi}AI assistant", GroupOrder = 1)]
    public System.Action TestConnection { get; set; }

    [SettingPropertyBool("{=EbiMcmJsonFormat}Request JSON response format", Order = 6, RequireRestart = false,
        HintText = "{=EbiMcmJsonFormatHint}Ask OpenAI-compatible backends for guaranteed JSON. Backends that reject it are detected and worked around automatically.")]
    [SettingPropertyGroup("{=EbiMcmGroupAi}AI assistant", GroupOrder = 1)]
    public bool AiUseJsonResponseFormat
    {
        get => S.Ai.UseJsonResponseFormat;
        set { S.Ai.UseJsonResponseFormat = value; Persist(); }
    }
}
