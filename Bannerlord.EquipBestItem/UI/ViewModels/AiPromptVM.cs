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
    private readonly Action _onApplied;

    private CancellationTokenSource? _pendingRequest;

    private string _promptText = "";
    private string _statusText = "";
    private bool _isBusy;

    public AiPromptVM(
        IRequestInterpreter interpreter,
        ProfileService profiles,
        InventoryGateway gateway,
        Action onApplied,
        bool isConfigured)
    {
        _interpreter = interpreter;
        _profiles = profiles;
        _gateway = gateway;
        _onApplied = onApplied;
        IsConfigured = isConfigured;
    }

    [DataSourceProperty]
    public bool IsConfigured { get; }

    [DataSourceProperty]
    public string InterpretButtonText { get; } =
        new TextObject("{=EbiAiInterpret}Ask AI").ToString();

    [DataSourceProperty]
    public string PromptText
    {
        get => _promptText;
        set
        {
            if (value == _promptText) return;
            _promptText = value;
            OnPropertyChangedWithValue(value);
        }
    }

    [DataSourceProperty]
    public string StatusText
    {
        get => _statusText;
        set
        {
            if (value == _statusText) return;
            _statusText = value;
            OnPropertyChangedWithValue(value);
        }
    }

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

    public void ExecuteInterpret()
    {
        var request = _promptText?.Trim() ?? "";
        if (request.Length == 0 || _isBusy) return;

        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;
        if (character is null || equipment is null) return;

        var context = new InterpretationContext(
            character.Name.ToString(),
            equipment.IsCivilian ? "civilian" : equipment.IsStealth ? "stealth" : "battle",
            CollectNotableSkills(character));

        _pendingRequest?.Cancel();
        var cancellation = _pendingRequest = new CancellationTokenSource();

        IsBusy = true;
        StatusText = new TextObject("{=EbiAiThinking}Interpreting request...").ToString();

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
                        StatusText = exception.Message;
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
                StatusText = exception is OperationCanceledException
                    ? new TextObject("{=EbiAiTimeout}The AI did not respond in time.").ToString()
                    : exception.Message;
                IsBusy = false;
            }
        });
    }

    /// <summary>
    ///     Writes each directive into the slot's filter — the same path the
    ///     manual sliders use — then recomputes the slot-button previews.
    ///     Runs on the main thread only.
    /// </summary>
    private void ApplyPlan(InterpretedPlan plan, CharacterObject character, Equipment equipment)
    {
        var changes = new List<string>();

        foreach (var directive in plan.Directives)
        {
            // Directives without explicit weights fall back to the slot
            // defaults. (culture/maxItemWeight have no filter field yet, so
            // they are not persisted here.)
            if (directive.Query.HasExplicitWeights)
                _profiles.SetWeights(character, equipment, directive.Slot, directive.Query.Weights);
            else
                _profiles.ResetSlot(character, equipment, directive.Slot);

            _profiles.SetWeaponClass(character, equipment, directive.Slot, directive.Query.WeaponClass);
            changes.Add(DescribeChange(directive));
        }

        _profiles.Save();
        _onApplied();

        var summary = string.Join("; ", changes);
        StatusText = plan.Explanation.Length > 0 ? $"{plan.Explanation} {summary}" : summary;
    }

    /// <summary>"Helm: Head Armor +1, Weight -0.5" in the game's language.</summary>
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
        else
        {
            parts.Add(new TextObject("{=ebi_default}Default").ToString());
        }

        if (directive.Query.WeaponClass is { } weaponClass)
            parts.Add(GameTexts.FindText("str_inventory_weapon", weaponClass.ToString()).ToString());

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
