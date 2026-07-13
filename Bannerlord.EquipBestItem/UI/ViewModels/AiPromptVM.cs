using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Bannerlord.EquipBestItem.Ai;
using Bannerlord.EquipBestItem.Domain;
using Bannerlord.EquipBestItem.Inventory;
using Bannerlord.EquipBestItem.Profiles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.EquipBestItem.UI.ViewModels;

/// <summary>
///     Free-text request box. The interpreter turns the text into a plan on a
///     background thread; the plan is then applied automatically on the main
///     thread (via <see cref="MainThread" />) — it rewrites the affected slot
///     filters the same way the manual sliders do and recomputes the previews.
///     The status line reports exactly which slots changed and how.
/// </summary>
public sealed class AiPromptVM : ViewModel
{
    private readonly IRequestInterpreter _interpreter;
    private readonly ProfileService _profiles;
    private readonly InventoryGateway _gateway;
    private readonly Settings.ModSettings _settings;
    private readonly Action _onApplied;

    private CancellationTokenSource? _pendingRequest;

    private string _lastRequest = "";
    private bool _isBusy;

    public AiPromptVM(
        IRequestInterpreter interpreter,
        ProfileService profiles,
        InventoryGateway gateway,
        Settings.ModSettings settings,
        Action onApplied)
    {
        _interpreter = interpreter;
        _profiles = profiles;
        _gateway = gateway;
        _settings = settings;
        _onApplied = onApplied;
        IsConfigured = settings.Ai.IsConfigured;
    }

    [DataSourceProperty]
    public bool IsConfigured { get; }

    [DataSourceProperty]
    public HintViewModel AskHint { get; } = new(new TextObject("{=EbiAiInterpret}Ask AI"));

    [DataSourceProperty]
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (value == _isBusy) return;
            _isBusy = value;
            OnPropertyChangedWithValue(value);
        }
    }

    /// <summary>
    ///     Opens the game's native text inquiry — proper keyboard focus, no
    ///     inventory hotkeys firing mid-typing — prefilled with the previous
    ///     request so it is easy to iterate on.
    /// </summary>
    public void ExecuteOpenPrompt()
    {
        if (_isBusy) return;

        InformationManager.ShowTextInquiry(new TextInquiryData(
            new TextObject("{=EbiAiInterpret}Ask AI").ToString(),
            new TextObject("{=EbiAiPromptHint}Describe the gear you want, in your own words.").ToString(),
            true, true,
            GameTexts.FindText("str_ok").ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            Interpret,
            null,
            textCondition: text => Tuple.Create(!string.IsNullOrWhiteSpace(text), ""),
            defaultInputText: _lastRequest));
    }

    private void Interpret(string requestText)
    {
        var request = requestText?.Trim() ?? "";
        if (request.Length == 0 || _isBusy) return;

        _lastRequest = request;

        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;
        if (character is null || equipment is null) return;

        var context = new InterpretationContext(
            character.Name.ToString(),
            equipment.IsCivilian ? "civilian" : equipment.IsStealth ? "stealth" : "battle",
            CollectNotableSkills(character),
            PromptGlossary.Text,
            CollectPartyHeroes(),
            MBTextManager.ActiveTextLanguage,
            _settings.UsePriority ? "priority" : _settings.UseEffectiveness ? "effectiveness" : "weights");

        _pendingRequest?.Cancel();
        var cancellation = _pendingRequest = new CancellationTokenSource();

        IsBusy = true;
        GameLog.Info(new TextObject("{=EbiAiThinking}Interpreting request...").ToString());

        Task.Run(async () =>
        {
            try
            {
                var plan = await _interpreter.InterpretAsync(request, context, cancellation.Token);
                if (cancellation.IsCancellationRequested) return;

                // Filters and previews touch game state, so the plan is
                // applied on the main thread on the next frame.
                MainThread.Post(() =>
                {
                    if (cancellation.IsCancellationRequested) return;
                    try
                    {
                        ApplyPlan(plan, character, equipment);
                    }
                    catch (Exception exception)
                    {
                        GameLog.Error(exception.Message);
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                });
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // Superseded by a newer request or the screen closed; whoever
                // cancelled owns the UI state.
            }
            catch (Exception exception)
            {
                // Includes the interpreter's own timeout, which surfaces as an
                // OperationCanceledException while our token is NOT cancelled.
                var message = exception is OperationCanceledException
                    ? new TextObject("{=EbiAiTimeout}The AI did not respond in time.").ToString()
                    : exception.Message;

                // Off the game thread here; the log and IsBusy touch UI state.
                MainThread.Post(() =>
                {
                    GameLog.Error(message);
                    IsBusy = false;
                });
            }
        });
    }

    /// <summary>
    ///     Writes each directive into the slot's filter — the same path the
    ///     manual sliders use — for every hero the plan targets, then
    ///     recomputes the slot-button previews. Non-current heroes get their
    ///     battle set edited. Runs on the main thread only.
    /// </summary>
    private void ApplyPlan(InterpretedPlan plan, CharacterObject character, Equipment equipment)
    {
        var targets = ResolveTargets(plan.Target, character, equipment);
        if (targets.Count == 0)
        {
            GameLog.Warn(new TextObject("{=EbiAiTargetNotFound}Could not find that hero in the party.").ToString());
            return;
        }

        var changes = new List<string>();
        foreach (var directive in plan.Directives)
            changes.Add(DescribeChange(directive));

        foreach (var (targetCharacter, targetEquipment) in targets)
        foreach (var directive in plan.Directives)
        {
            var hasWeights = directive.Query.HasExplicitWeights;
            var hasPriorities = directive.Query.Priorities is { Count: > 0 };

            // Directives without explicit preferences fall back to the slot defaults.
            if (!hasWeights && !hasPriorities)
                _profiles.ResetSlot(targetCharacter, targetEquipment, directive.Slot);
            if (hasWeights)
                _profiles.SetWeights(targetCharacter, targetEquipment, directive.Slot, directive.Query.Weights);
            if (hasPriorities)
                _profiles.SetPriorities(targetCharacter, targetEquipment, directive.Slot, directive.Query.Priorities);

            _profiles.SetWeaponCategory(targetCharacter, targetEquipment, directive.Slot, directive.Query.WeaponCategory);
            _profiles.SetConstraints(
                targetCharacter, targetEquipment, directive.Slot,
                directive.Query.CultureId, directive.Query.MaxItemWeight);
        }

        _profiles.Save();
        _onApplied();

        var summary = string.Join("; ", changes);
        if (targets.Count > 1) summary += $" (x{targets.Count})";

        // The assistant's answer, then the concrete per-slot changes.
        if (plan.Explanation.Length > 0) GameLog.Ai(plan.Explanation);
        GameLog.Ai(summary);
    }

    /// <summary>
    ///     "current" (or empty) → the shown character with the open set;
    ///     "others"/"all" → party heroes (their battle sets); anything else is
    ///     matched as a hero name. Empty result = named hero not found.
    /// </summary>
    private List<(CharacterObject Character, Equipment Equipment)> ResolveTargets(
        string target, CharacterObject current, Equipment currentEquipment)
    {
        var kind = target.Trim().ToLowerInvariant();
        if (kind.Length == 0 || kind == "current")
            return new List<(CharacterObject, Equipment)> { (current, currentEquipment) };

        var result = new List<(CharacterObject, Equipment)>();
        foreach (var hero in _gateway.GetEquippableHeroes())
        {
            var include = kind switch
            {
                "all" => true,
                "others" => hero.HeroObject != Hero.MainHero,
                _ => string.Equals(hero.Name.ToString(), target.Trim(), StringComparison.OrdinalIgnoreCase)
            };
            if (!include) continue;

            var heroEquipment = hero == current ? currentEquipment : hero.FirstBattleEquipment;
            if (heroEquipment is not null) result.Add((hero, heroEquipment));
        }

        return result;
    }

    /// <summary>Equippable party heroes for the prompt, the main hero marked.</summary>
    private IReadOnlyList<string> CollectPartyHeroes()
    {
        var names = new List<string>();
        foreach (var hero in _gateway.GetEquippableHeroes())
        {
            var name = hero.Name.ToString();
            if (hero.HeroObject == Hero.MainHero) name += " (main)";
            names.Add(name);
        }

        return names;
    }

    /// <summary>"Helm: Head Armor +1, Weight -0.5" (or "Hit Points > Speed = Armor") in the game's language.</summary>
    private static string DescribeChange(EquipDirective directive)
    {
        var parts = new List<string>();

        if (directive.Query.HasExplicitWeights)
        {
            for (var i = 0; i < ItemParams.Count; i++)
            {
                var param = (ItemParam)i;
                var value = directive.Query.Weights[param];
                if (value != 0f)
                    parts.Add(SlotWeightsVM.GetParamName(param) + " " +
                              value.ToString("+0.##;-0.##", CultureInfo.InvariantCulture));
            }
        }

        if (directive.Query.Priorities is { Count: > 0 } priorities)
        {
            var ranks = new List<string>(priorities.Count);
            foreach (var group in priorities)
            {
                var names = new List<string>(group.Count);
                foreach (var param in group) names.Add(SlotWeightsVM.GetParamName(param));
                ranks.Add(string.Join(" = ", names));
            }

            parts.Add(string.Join(" > ", ranks));
        }

        if (parts.Count == 0)
            parts.Add(new TextObject("{=ebi_default}Default").ToString());

        if (directive.Query.WeaponCategory is { } category)
            parts.Add(SlotWeightsVM.GetCategoryName(category));

        if (directive.Query.CultureId is { } cultureId)
            parts.Add(SlotWeightsVM.GetCultureName(cultureId));

        if (directive.Query.MaxItemWeight > 0f)
            parts.Add("<= " + directive.Query.MaxItemWeight.ToString("0.#", CultureInfo.InvariantCulture) + " " +
                      SlotWeightsVM.GetParamName(ItemParam.Weight).ToLowerInvariant());

        return $"{SlotWeightsVM.GetSlotName(directive.Slot)}: {string.Join(", ", parts)}";
    }

    public override void OnFinalize()
    {
        _pendingRequest?.Cancel();
        base.OnFinalize();
    }

    private static IReadOnlyList<string> CollectNotableSkills(CharacterObject character)
    {
        var skills = new List<string>();

        foreach (var skill in new[]
                 {
                     DefaultSkills.OneHanded, DefaultSkills.TwoHanded, DefaultSkills.Polearm,
                     DefaultSkills.Bow, DefaultSkills.Crossbow, DefaultSkills.Throwing,
                     DefaultSkills.Riding, DefaultSkills.Athletics
                 })
        {
            var value = character.GetSkillValue(skill);
            if (value >= 30) skills.Add($"{skill.Name}: {value}");
        }

        return skills;
    }
}
