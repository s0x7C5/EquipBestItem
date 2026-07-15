using System.Collections.Generic;
using System.Globalization;
using Bannerlord.EquipBestItem.Domain.Explaining;
using Bannerlord.EquipBestItem.UI.ViewModels;
using TaleWorlds.Localization;

namespace Bannerlord.EquipBestItem.UI;

/// <summary>
///     Turns a <see cref="SearchExplanation" /> (pure facts) into localized
///     lines in the game message log. Kept in the UI layer because it needs
///     the localized parameter/slot names and the message log.
/// </summary>
internal static class ExplanationFormatter
{
    public static void Log(SearchExplanation e)
    {
        var slot = SlotWeightsVM.GetSlotName(e.Slot);

        switch (e.Kind)
        {
            case ExplanationKind.SlotLocked:
                GameLog.Ai($"{slot}: {Text("{=EbiWhySlotLocked}This slot is excluded from searching.")}");
                return;
            case ExplanationKind.NothingBetter:
                GameLog.Ai($"{slot}: {NothingBetterText(e)}");
                return;
            case ExplanationKind.FirstItem:
                GameLog.Ai($"{slot}: {FirstItemText(e)}");
                return;
            case ExplanationKind.NamedNotFound:
                GameLog.Ai($"{slot}: {Var("{=EbiWhyNamedNotFound}\"{ITEM}\" is not in your searched inventory.", "ITEM", e.QueriedName)}");
                return;
            case ExplanationKind.NamedIsBest:
                GameLog.Ai($"{slot}: {Var("{=EbiWhyNamedIsBest}\"{ITEM}\" is exactly what's recommended here.", "ITEM", e.CurrentItemName)}");
                return;
            case ExplanationKind.NamedFilteredOut:
                GameLog.Ai($"{slot}: {FilteredOutText(e)}");
                return;
        }

        // Upgrade, NamedLoses, NamedMarginal — a winner and a stat breakdown.
        GameLog.Ai($"{slot}: {WinnerHeadline(e)}");

        if (e.Mode == SearchMode.Effectiveness)
        {
            GameLog.Ai(EffectivenessText(e));
            return;
        }

        foreach (var line in DetailLines(e))
            GameLog.Ai(line);

        // The tweak only makes sense when raising a weight/rank would flip the pick.
        if (e.TweakParam is { } tweak && e.Kind is ExplanationKind.Upgrade or ExplanationKind.NamedLoses)
            GameLog.Info(TweakText(e.Mode, tweak));
    }

    private static string WinnerHeadline(SearchExplanation e) => e.Kind switch
    {
        ExplanationKind.NamedLoses => Text("{=EbiWhyNamedLoses}{WINNER} is picked over \"{ITEM}\"")
            .SetTextVariable("WINNER", e.FoundItemName).SetTextVariable("ITEM", e.CurrentItemName).ToString(),
        ExplanationKind.NamedMarginal => Text(
                "{=EbiWhyNamedMarginal}\"{ITEM}\" scores a bit higher than {CURRENT}, but not by the clear margin needed to suggest a swap.")
            .SetTextVariable("ITEM", e.FoundItemName).SetTextVariable("CURRENT", e.CurrentItemName).ToString(),
        _ => HeadlineText(e)
    };

    private static string FilteredOutText(SearchExplanation e) =>
        Text("{=EbiWhyNamedFiltered}\"{ITEM}\" doesn't qualify: {REASON}")
            .SetTextVariable("ITEM", e.FoundItemName)
            .SetTextVariable("REASON", ReasonText(e.FilterReason)).ToString();

    private static string ReasonText(RejectionReason reason) => reason switch
    {
        RejectionReason.WrongTypeForSlot => Text("{=EbiRejectType}wrong item type for this slot").ToString(),
        RejectionReason.WrongWeaponClass => Text("{=EbiRejectWeaponClass}wrong weapon type for this slot").ToString(),
        RejectionReason.ShieldAlreadyEquipped => Text("{=EbiRejectShield}a shield is already in another weapon slot").ToString(),
        RejectionReason.NotEquippable => Text("{=EbiRejectEquip}this hero can't equip it here").ToString(),
        RejectionReason.OverWeightLimit => Text("{=EbiRejectWeight}over the slot's weight limit").ToString(),
        RejectionReason.WrongCulture => Text("{=EbiRejectCulture}wrong culture for the slot's filter").ToString(),
        _ => Text("{=EbiRejectOther}it does not match the slot's filter").ToString()
    };

    private static string Var(string idAndFallback, string name, string value) =>
        new TextObject(idAndFallback).SetTextVariable(name, value).ToString();

    private static IEnumerable<string> DetailLines(SearchExplanation explanation)
    {
        if (explanation.Mode == SearchMode.Priority)
        {
            var ties = Collect(explanation, FactorRole.Tie, Raw);
            var decided = Collect(explanation, FactorRole.Decides, Raw);

            if (decided.Length > 0)
                yield return Text("{=EbiWhyDecidedBy}Decided by {STATS}").SetTextVariable("STATS", decided).ToString();
            if (ties.Length > 0)
                yield return Text("{=EbiWhyTiedOn}Tied on {STATS}; lower priorities were not checked")
                    .SetTextVariable("STATS", ties).ToString();
            yield break;
        }

        var ahead = Collect(explanation, FactorRole.Advantage, Percentiles);
        var behind = Collect(explanation, FactorRole.Disadvantage, Percentiles);

        if (ahead.Length > 0)
            yield return Text("{=EbiWhyAhead}Ahead: {STATS}").SetTextVariable("STATS", ahead).ToString();
        if (behind.Length > 0)
            yield return Text("{=EbiWhyBehind}Behind (counts less): {STATS}").SetTextVariable("STATS", behind).ToString();
    }

    private static string HeadlineText(SearchExplanation e) =>
        Text("{=EbiWhyUpgrade}{FOUND} beats {CURRENT}")
            .SetTextVariable("FOUND", e.FoundItemName)
            .SetTextVariable("CURRENT", e.CurrentItemName).ToString();

    private static string FirstItemText(SearchExplanation e) =>
        Text("{=EbiWhyFirstItem}{FOUND} for an empty slot.").SetTextVariable("FOUND", e.FoundItemName).ToString();

    private static string NothingBetterText(SearchExplanation e) =>
        e.CurrentItemName.Length == 0
            ? Text("{=EbiWhyNothingFound}Nothing found for this slot.").ToString()
            : Text("{=EbiWhyNothingBetter}Nothing clearly better than {CURRENT}.")
                .SetTextVariable("CURRENT", e.CurrentItemName).ToString();

    private static string EffectivenessText(SearchExplanation e) =>
        Text("{=EbiWhyEffectiveness}The game rates it {FOUND} vs {CURRENT} overall — its own score, with no stat breakdown.")
            .SetTextVariable("FOUND", e.FoundScore.ToString("0", CultureInfo.InvariantCulture))
            .SetTextVariable("CURRENT", e.CurrentScore.ToString("0", CultureInfo.InvariantCulture)).ToString();

    private static string TweakText(SearchMode mode, Domain.ItemParam param)
    {
        var name = SlotWeightsVM.GetParamName(param);
        return mode == SearchMode.Priority
            ? Text("{=EbiWhyTweakPriority}Tip: move {STAT} up to prioritize it.").SetTextVariable("STAT", name).ToString()
            : Text("{=EbiWhyTweakWeight}Tip: give {STAT} more weight to value it more.").SetTextVariable("STAT", name).ToString();
    }

    private static string Collect(SearchExplanation explanation, FactorRole role, System.Func<ExplanationFactor, string> render)
    {
        var parts = new List<string>();
        foreach (var factor in explanation.Factors)
            if (factor.Role == role)
                parts.Add(render(factor));
        return string.Join(", ", parts);
    }

    private static string Percentiles(ExplanationFactor f) =>
        $"{SlotWeightsVM.GetParamName(f.Param)} {f.FoundPercentile}% vs {f.CurrentPercentile}%";

    private static string Raw(ExplanationFactor f) =>
        $"{SlotWeightsVM.GetParamName(f.Param)} {f.FoundValue.ToString("0", CultureInfo.InvariantCulture)} " +
        $"vs {f.CurrentValue.ToString("0", CultureInfo.InvariantCulture)}";

    private static TextObject Text(string idAndFallback) => new(idAndFallback);

    /// <summary>
    ///     A compact, English, stable-identifier line for the AI prompt, or ""
    ///     for slots with no recommendation to narrate. Item names stay as
    ///     shown; stats use enum names so the model can cite them precisely.
    /// </summary>
    public static string ToPromptFact(SearchExplanation e)
    {
        if (e.Kind != ExplanationKind.Upgrade && e.Kind != ExplanationKind.FirstItem) return "";

        var slot = e.Slot.ToString();
        if (e.Kind == ExplanationKind.FirstItem)
            return $"{slot}: best is \"{e.FoundItemName}\" for an empty slot.";

        var head = $"{slot}: best is \"{e.FoundItemName}\" over \"{e.CurrentItemName}\"";

        if (e.Mode == SearchMode.Effectiveness)
            return $"{head} by game Effectiveness {e.FoundScore:0} vs {e.CurrentScore:0} (no stat breakdown).";

        if (e.Mode == SearchMode.Priority)
        {
            var decided = PromptFactors(e, FactorRole.Decides, PromptRaw);
            var ties = PromptFactors(e, FactorRole.Tie, PromptRaw);
            var parts = new List<string>();
            if (decided.Length > 0) parts.Add("decided by " + decided);
            if (ties.Length > 0) parts.Add("tied on " + ties + " (lower ranks not checked)");
            return $"{head}: {string.Join("; ", parts)}.";
        }

        var ahead = PromptFactors(e, FactorRole.Advantage, PromptPercentiles);
        var behind = PromptFactors(e, FactorRole.Disadvantage, PromptPercentiles);
        var weightParts = new List<string>();
        if (ahead.Length > 0) weightParts.Add("ahead " + ahead);
        if (behind.Length > 0) weightParts.Add("behind " + behind);
        return $"{head}: {string.Join("; ", weightParts)}.";
    }

    private static string PromptFactors(SearchExplanation e, FactorRole role, System.Func<ExplanationFactor, string> render)
    {
        var parts = new List<string>();
        foreach (var factor in e.Factors)
            if (factor.Role == role)
                parts.Add(render(factor));
        return string.Join(", ", parts);
    }

    private static string PromptPercentiles(ExplanationFactor f) =>
        $"{f.Param} {f.FoundPercentile}%/{f.CurrentPercentile}%";

    private static string PromptRaw(ExplanationFactor f) =>
        $"{f.Param} {f.FoundValue.ToString("0", CultureInfo.InvariantCulture)}/" +
        $"{f.CurrentValue.ToString("0", CultureInfo.InvariantCulture)}";
}
