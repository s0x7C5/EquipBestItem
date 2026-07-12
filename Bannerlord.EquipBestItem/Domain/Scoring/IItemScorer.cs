using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Scoring;

public interface IItemScorer
{
    float Score(EquipmentElement element, in SearchContext context);

    /// <summary>
    ///     Whether the candidate is enough of an upgrade over the currently
    ///     equipped item to be worth suggesting — scorers can demand a margin
    ///     or stat dominance on top of a bare score win, so near-ties and
    ///     lopsided trade-offs do not produce noisy swap suggestions.
    /// </summary>
    bool BeatsCurrent(EquipmentElement candidate, EquipmentElement current, in SearchContext context);
}
