using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Scoring;

/// <summary>
///     Stat vectors cached per (item, modifier) pair until the inventory is
///     refreshed, shared by every scoring strategy so repeated searches stay
///     O(items) with no re-extraction.
/// </summary>
public sealed class ItemStatCache
{
    private readonly Dictionary<CacheKey, float[]> _cache = new();
    private EquipmentElement _cachedHarness;

    public float[] GetStats(EquipmentElement element, EquipmentElement harness)
    {
        // Mount stats depend on the equipped harness; a harness change invalidates them.
        if (!Equals(_cachedHarness.Item, harness.Item))
        {
            _cache.Clear();
            _cachedHarness = harness;
        }

        var key = new CacheKey(element.Item, element.ItemModifier);
        if (_cache.TryGetValue(key, out var stats)) return stats;

        stats = new float[ItemParams.Count];
        ItemStatExtractor.Fill(element, harness, stats);
        _cache[key] = stats;
        return stats;
    }

    /// <summary>Drop cached stats. Call when the inventory contents change.</summary>
    public void Invalidate()
    {
        _cache.Clear();
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
