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
    public static void Log(SearchExplanation explanation)
    {
        var slot = SlotWeightsVM.GetSlotName(explanation.Slot);

        switch (explanation.Kind)
        {
            case ExplanationKind.SlotLocked:
                GameLog.Ai($"{slot}: {Text("{=EbiWhySlotLocked}This slot is excluded from searching.")}");
                return;

            case ExplanationKind.NothingBetter:
                GameLog.Ai($"{slot}: {NothingBetterText(explanation)}");
                return;

            case ExplanationKind.FirstItem:
                GameLog.Ai($"{slot}: {FirstItemText(explanation)}");
                return;
        }

        // Upgrade.
        GameLog.Ai($"{slot}: {HeadlineText(explanation)}");

        if (explanation.Mode == SearchMode.Effectiveness)
        {
            GameLog.Ai(EffectivenessText(explanation));
            return;
        }

        foreach (var line in DetailLines(explanation))
            GameLog.Ai(line);

        if (explanation.TweakParam is { } tweak)
            GameLog.Info(TweakText(explanation.Mode, tweak));
    }

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
}
