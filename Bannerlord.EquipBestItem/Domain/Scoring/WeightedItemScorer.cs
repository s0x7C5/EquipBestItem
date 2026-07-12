using System;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Scoring;

/// <summary>
///     Weighted sum of item stats with diminishing returns:
///     sum(w * sqrt(percentile(v))) / sum(|w|), each stat first scored as its
///     percentile within the item's class over the whole catalog
///     (<see cref="ItemStatPercentiles" />) — self-calibrating, mod-aware,
///     and unit-free. The square root makes gains near the bottom of a stat
///     worth more than gains near the top, so balanced items beat
///     one-huge-stat freaks — a linear sum lets any surplus buy off any
///     deficit, which is not how a player judges gear. The score depends
///     only on the item, never on what else is in the inventory, so picks
///     are stable across equips.
/// </summary>
public sealed class WeightedItemScorer : IItemScorer
{
    // A trade-off candidate must clear the current item by this relative
    // margin (with a small absolute floor near zero scores) before it is
    // suggested; stat-dominant candidates pass without it. Kills both the
    // "swapped two near-identical shields" churn and lopsided trades.
    private const float UpgradeMargin = 0.05f;
    private const float MinUpgradeStep = 0.01f;
    private const float StatEpsilon = 0.001f;

    private readonly ItemStatCache _stats;
    private readonly ItemStatPercentiles _percentiles;

    public WeightedItemScorer(ItemStatCache stats, ItemStatPercentiles percentiles)
    {
        _stats = stats;
        _percentiles = percentiles;
    }

    public float Score(EquipmentElement element, in SearchContext context)
    {
        var weights = context.Query.Weights.Raw;
        var stats = _stats.GetStats(element, context.Equipment[EquipmentIndex.HorseHarness]);

        var weightedSum = 0f;
        var weightTotal = 0f;

        for (var i = 0; i < weights.Length; i++)
        {
            var weight = weights[i];
            if (weight == 0f) continue;

            var normalized = _percentiles.Normalize(element.Item, i, stats[i]);
            weightedSum += (float)Math.Sqrt(normalized) * weight;
            weightTotal += Math.Abs(weight);
        }

        return weightTotal > 0f ? weightedSum / weightTotal : 0f;
    }

    /// <summary>
    ///     A candidate that is at least as good on every weighted stat (and
    ///     better overall) is always a safe suggestion. A trade-off candidate
    ///     must clear the score margin instead.
    /// </summary>
    public bool BeatsCurrent(EquipmentElement candidate, EquipmentElement current, in SearchContext context)
    {
        var candidateScore = Score(candidate, context);
        var currentScore = Score(current, context);
        if (candidateScore <= currentScore) return false;

        if (candidateScore >= currentScore + Math.Max(Math.Abs(currentScore) * UpgradeMargin, MinUpgradeStep))
            return true;

        // Within the margin: only a dominating candidate (no downside on any
        // stat the player weighs) is suggested.
        var weights = context.Query.Weights.Raw;
        var harness = context.Equipment[EquipmentIndex.HorseHarness];
        var candidateStats = _stats.GetStats(candidate, harness);
        var currentStats = _stats.GetStats(current, harness);

        for (var i = 0; i < weights.Length; i++)
        {
            var weight = weights[i];
            if (weight == 0f) continue;

            var gain = (candidateStats[i] - currentStats[i]) * Math.Sign(weight);
            if (gain < -StatEpsilon) return false;
        }

        return true;
    }
}
