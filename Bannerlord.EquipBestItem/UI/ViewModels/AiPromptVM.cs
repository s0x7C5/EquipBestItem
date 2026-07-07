using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bannerlord.EquipBestItem.Ai;
using Bannerlord.EquipBestItem.Inventory;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.EquipBestItem.UI.ViewModels;

/// <summary>
///     Free-text request box: the interpreter turns the text into a plan on a
///     background thread, the player applies the plan with a second click on
///     the main thread — so game state is never mutated off-thread.
/// </summary>
public sealed class AiPromptVM : ViewModel
{
    private readonly IRequestInterpreter _interpreter;
    private readonly EquipBestService _equipBest;
    private readonly InventoryGateway _gateway;

    private InterpretedPlan? _plan;
    private CancellationTokenSource? _pendingRequest;

    private string _promptText = "";
    private string _statusText = "";
    private bool _isBusy;

    public AiPromptVM(
        IRequestInterpreter interpreter,
        EquipBestService equipBest,
        InventoryGateway gateway,
        bool isConfigured)
    {
        _interpreter = interpreter;
        _equipBest = equipBest;
        _gateway = gateway;
        IsConfigured = isConfigured;
    }

    [DataSourceProperty]
    public bool IsConfigured { get; }

    [DataSourceProperty]
    public string InterpretButtonText { get; } =
        new TextObject("{=EbiAiInterpret}Ask AI").ToString();

    [DataSourceProperty]
    public string ApplyButtonText { get; } =
        new TextObject("{=EbiAiApply}Apply").ToString();

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

    [DataSourceProperty]
    public bool CanApply => _plan is not null && !_isBusy;

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

        _plan = null;
        IsBusy = true;
        StatusText = new TextObject("{=EbiAiThinking}Interpreting request...").ToString();
        OnPropertyChanged(nameof(CanApply));

        Task.Run(async () =>
        {
            try
            {
                var plan = await _interpreter.InterpretAsync(request, context, cancellation.Token);
                if (cancellation.IsCancellationRequested) return;

                // Property updates are picked up by Gauntlet on the next frame;
                // no game state is touched from this thread.
                _plan = plan;
                StatusText = plan.Explanation.Length > 0
                    ? plan.Explanation
                    : new TextObject("{=EbiAiReady}Plan ready, press Apply.").ToString();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                StatusText = exception.Message;
            }
            finally
            {
                if (!cancellation.IsCancellationRequested)
                {
                    IsBusy = false;
                    OnPropertyChanged(nameof(CanApply));
                }
            }
        });
    }

    public void ExecuteApply()
    {
        var plan = _plan;
        if (plan is null || _isBusy) return;

        var equippedCount = 0;
        foreach (var directive in plan.Directives)
            if (_equipBest.TryEquipBest(_gateway, directive.Query, directive.Slot) is not null)
                equippedCount++;

        StatusText = new TextObject("{=EbiAiApplied}Equipped {COUNT} item(s).")
            .SetTextVariable("COUNT", equippedCount).ToString();
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
