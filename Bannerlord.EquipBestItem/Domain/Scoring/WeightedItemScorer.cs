using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Scoring;

/// <summary>
///     Normalized weighted sum of item stats: sum(w * v) / sum(|w|).
///     Stat vectors are cached per (item, modifier) pair until the inventory
///     is refreshed, so repeated searches stay O(items) with no re-extraction.
/// </summary>
public sealed class WeightedItemScorer : IItemScorer
{
    private readonly Dictionary<CacheKey, float[]> _statCache = new();
    private EquipmentElement _cachedHarness;

    public float Score(EquipmentElement element, in SearchContext context)
    {
        var weights = context.Query.Weights.Raw;
        var stats = GetStats(element, context.Equipment[EquipmentIndex.HorseHarness]);

        var weightedSum = 0f;
        var weightTotal = 0f;

        for (var i = 0; i < weights.Length; i++)
        {
            var weight = weights[i];
            if (weight == 0f) continue;

            weightedSum += stats[i] * weight;
            weightTotal += Math.Abs(weight);
        }

        return weightTotal > 0f ? weightedSum / weightTotal : 0f;
    }

    /// <summary>Drop cached stats. Call when the inventory contents change.</summary>
    public void InvalidateCache()
    {
        _statCache.Clear();
    }

    private float[] GetStats(EquipmentElement element, EquipmentElement harness)
    {
        // Mount stats depend on the equipped harness; a harness change invalidates them.
        if (!Equals(_cachedHarness.Item, harness.Item))
        {
            _statCache.Clear();
            _cachedHarness = harness;
        }

        var key = new CacheKey(element.Item, element.ItemModifier);
        if (_statCache.TryGetValue(key, out var stats)) return stats;

        stats = new float[ItemParams.Count];
        ItemStatExtractor.Fill(element, harness, stats);
        _statCache[key] = stats;
        return stats;
    }

    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        private readonly ItemObject? _item;
        private readonly ItemModifier? _modifier;

        public CacheKey(ItemObject? item, ItemModifier? modifier)
        {
            _item = item;
            _modifier = modifier;
        }

        public bool Equals(CacheKey other) =>
            ReferenceEquals(_item, other._item) && ReferenceEquals(_modifier, other._modifier);

        public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_item?.GetHashCode() ?? 0) * 397) ^ (_modifier?.GetHashCode() ?? 0);
            }
        }
    }
}
