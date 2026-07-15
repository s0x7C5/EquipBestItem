using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Bannerlord.EquipBestItem.Domain.Scoring;

/// <summary>
///     Per-parameter stat distributions over the whole item catalog, grouped
///     by comparable kind: weapon class for weapons and shields, item type
///     for armor and mounts. A stat is then scored as its percentile within
///     its group — "top 10% of shields by hit points" — which puts every
///     parameter on a common, self-calibrating 0..1 range: no hand-tuned
///     reference scales, and modded items reshape the distributions
///     automatically. The lookup only needs the group and the value, so
///     items absent from the table (fresh player-crafted weapons) still
///     normalize against their class's distribution.
///
///     Built lazily from MBObjectManager on first use; <see cref="Refresh" />
///     cheaply re-checks the catalog size each time the inventory changes,
///     since crafting registers new items mid-campaign.
/// </summary>
public sealed class ItemStatPercentiles
{
    private readonly Dictionary<int, float[][]> _groups = new();
    private int _builtCount = -1;

    /// <summary>Rebuilds the tables when the item catalog changed size (cheap otherwise).</summary>
    public void Refresh()
    {
        var items = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
        if (items is null || items.Count == 0 || items.Count == _builtCount) return;

        Rebuild(items);
        _builtCount = items.Count;
    }

    /// <summary>
    ///     The stat's percentile (0..1) within the item's group; falls back to
    ///     the fixed reference scales when the group is unknown.
    /// </summary>
    public float Normalize(ItemObject? item, int param, float value)
    {
        var table = item is null ? null : FindTable(GroupKey(item), param);
        if (table is null)
            return Math.Min(1f, Math.Max(0f, value * ParamScales.Inverse[param]));

        // No spread = the stat cannot rank anyone in this group.
        if (table[0] >= table[table.Length - 1]) return 0.5f;

        // Fraction of the group at or below the value (binary upper bound).
        var low = 0;
        var high = table.Length;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (table[mid] <= value) low = mid + 1;
            else high = mid;
        }

        return (float)low / table.Length;
    }

    private float[]? FindTable(int group, int param)
    {
        if (!_groups.TryGetValue(group, out var byParam)) return null;

        var table = byParam[param];
        return table is { Length: > 0 } ? table : null;
    }

    /// <summary>Weapons and shields compare within their weapon class; the rest within their item type.</summary>
    private static int GroupKey(ItemObject item) =>
        item.PrimaryWeapon is { } weapon ? 1000 + (int)weapon.WeaponClass : (int)item.ItemType;

    private void Rebuild(MBReadOnlyList<ItemObject> items)
    {
        var vectors = new Dictionary<int, List<float[]>>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item is null) continue;

            var stats = new float[ItemParams.Count];
            ItemStatExtractor.Fill(new EquipmentElement(item), default, stats);

            var key = GroupKey(item);
            if (!vectors.TryGetValue(key, out var list))
                vectors[key] = list = new List<float[]>();
            list.Add(stats);
        }

        _groups.Clear();
        foreach (var pair in vectors)
        {
            var byParam = new float[ItemParams.Count][];
            for (var param = 0; param < ItemParams.Count; param++)
            {
                var values = new float[pair.Value.Count];
                for (var i = 0; i < pair.Value.Count; i++)
                    values[i] = pair.Value[i][param];
                Array.Sort(values);
                byParam[param] = values;
            }

            _groups[pair.Key] = byParam;
        }
    }
}
