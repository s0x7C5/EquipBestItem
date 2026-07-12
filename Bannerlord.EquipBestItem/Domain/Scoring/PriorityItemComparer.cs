using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Scoring;

/// <summary>
///     Lexicographic ranking by the slot's stat-priority order: candidates are
///     compared on the top-priority group first; only a tie falls through to
///     the next one. A group of one stat compares that raw stat; a group of
///     several equal-rank stats compares their combined value on the common
///     <see cref="ParamScales" /> range, so "hit points = armor" means their
///     sum decides, not whichever happens to use bigger numbers.
/// </summary>
public sealed class PriorityItemComparer : IItemComparer
{
    // Stats are ints at heart (armor, damage, speed); anything closer than
    // this is the same value wearing float rounding. Scaled group sums live
    // on a ~0..1 range where the same tolerance suits.
    private const float Epsilon = 0.001f;

    private readonly ItemStatCache _stats;

    public PriorityItemComparer(ItemStatCache stats)
    {
        _stats = stats;
    }

    public int Compare(EquipmentElement a, EquipmentElement b, in SearchContext context)
    {
        var harness = context.Equipment[EquipmentIndex.HorseHarness];
        var statsA = _stats.GetStats(a, harness);
        var statsB = _stats.GetStats(b, harness);

        // "As equipped" slots only ever compare items of the equipped item's
        // class, so its class-specific order applies — the generic weapon
        // order would rank a shield by thrust damage before hit points.
        var order = context.Query.Priorities
                    ?? DefaultPriorities.GroupsFor(context.Slot,
                        context.Query.WeaponCategory?.Class
                        ?? context.Equipment[context.Slot].Item?.PrimaryWeapon?.WeaponClass);

        for (var i = 0; i < order.Count; i++)
        {
            var group = order[i];
            float diff;

            if (group.Count == 1)
            {
                diff = statsA[(int)group[0]] - statsB[(int)group[0]];
            }
            else
            {
                diff = 0f;
                for (var j = 0; j < group.Count; j++)
                {
                    var param = (int)group[j];
                    diff += (statsA[param] - statsB[param]) * ParamScales.Inverse[param];
                }
            }

            if (diff > Epsilon) return 1;
            if (diff < -Epsilon) return -1;
        }

        return 0;
    }
}
