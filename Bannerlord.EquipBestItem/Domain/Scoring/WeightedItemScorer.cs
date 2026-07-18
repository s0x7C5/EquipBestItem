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
    // A trade-off candidate must clear the current item by a relative margin
    // (player-configurable, 0 = off) before it is suggested; stat-dominant
    // candidates pass without it. Kills both the "swapped two near-identical
    // shields" churn and lopsided trades. Scores near zero use the floor
    // instead, so the margin stays meaningful there (at 5% this reproduces
    // the historical fixed 0.01 step).
    private const float MarginScoreFloor = 0.2f;
    private const float StatEpsilon = 0.001f;

    private readonly ItemStatCache _stats;
    private readonly ItemStatPercentiles _percentiles;
    private readonly Func<float> _upgradeMargin;

    public WeightedItemScorer(ItemStatCache stats, ItemStatPercentiles percentiles, Func<float> upgradeMargin)
    {
        _stats = stats;
        _percentiles = percentiles;
        _upgradeMargin = upgradeMargin;
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
    ///     Each parameter's signed contribution to the score:
    ///     w·√percentile / Σ|w|. The sum equals <see cref="Score" /> exactly
    ///     (same formula), so an explanation built from this can never disagree
    ///     with the ranking. Zero-weight parameters contribute 0.
    /// </summary>
    public void Breakdown(EquipmentElement element, in SearchContext context, float[] into)
    {
        var weights = context.Query.Weights.Raw;
        var stats = _stats.GetStats(element, context.Equipment[EquipmentIndex.HorseHarness]);

        var weightTotal = 0f;
        for (var i = 0; i < weights.Length; i++)
            weightTotal += Math.Abs(weights[i]);

        for (var i = 0; i < into.Length; i++)
        {
            var weight = weights[i];
            into[i] = weight == 0f || weightTotal <= 0f
                ? 0f
                : (float)Math.Sqrt(_percentiles.Normalize(element.Item, i, stats[i])) * weight / weightTotal;
        }
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

        var margin = _upgradeMargin();
        if (margin <= 0f) return true; // guard disabled: any score win counts

        if (candidateScore >= currentScore + Math.Max(Math.Abs(currentScore), MarginScoreFloor) * margin)
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
