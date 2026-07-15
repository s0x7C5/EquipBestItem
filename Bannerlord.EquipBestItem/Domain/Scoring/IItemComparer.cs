using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Scoring;

/// <summary>
///     Pairwise ranking strategy for search modes that have no meaningful
///     scalar score (e.g. stat-priority ordering).
/// </summary>
public interface IItemComparer
{
    /// <returns>Positive when <paramref name="a" /> beats <paramref name="b" />, negative when it loses, 0 on a tie.</returns>
    int Compare(EquipmentElement a, EquipmentElement b, in SearchContext context);
}
