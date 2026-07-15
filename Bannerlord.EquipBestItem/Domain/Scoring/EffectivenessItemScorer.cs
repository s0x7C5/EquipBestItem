using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Scoring;

/// <summary>Scores items by the game's own aggregate Effectiveness value.</summary>
public sealed class EffectivenessItemScorer : IItemScorer
{
    public float Score(EquipmentElement element, in SearchContext context) =>
        element.Item?.Effectiveness ?? 0f;

    public bool BeatsCurrent(EquipmentElement candidate, EquipmentElement current, in SearchContext context) =>
        Score(candidate, context) > Score(current, context);
}
