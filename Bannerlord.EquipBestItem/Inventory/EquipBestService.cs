using Bannerlord.EquipBestItem.Domain;
using Bannerlord.EquipBestItem.Domain.Scoring;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Inventory;

/// <summary>
///     Application-level "find the best item for this slot and equip it".
///     Queries with an empty weight vector fall back to the game's
///     Effectiveness score.
/// </summary>
public sealed class EquipBestService
{
    private readonly BestItemFinder _finder;
    private readonly WeightedItemScorer _weightedScorer;
    private readonly EffectivenessItemScorer _effectivenessScorer;

    public EquipBestService(
        BestItemFinder finder,
        WeightedItemScorer weightedScorer,
        EffectivenessItemScorer effectivenessScorer)
    {
        _finder = finder;
        _weightedScorer = weightedScorer;
        _effectivenessScorer = effectivenessScorer;
    }

    /// <returns>The display name of the equipped item, or null when nothing better was found.</returns>
    public string? TryEquipBest(InventoryGateway gateway, ItemQuery query, EquipmentIndex slot)
    {
        var character = gateway.CurrentCharacter;
        var equipment = gateway.ActiveEquipment;
        if (character is null || equipment is null) return null;

        var context = new SearchContext(character, equipment, slot, query);
        var scorer = query.Weights.IsEmpty ? (IItemScorer)_effectivenessScorer : _weightedScorer;

        var best = _finder.FindBest(context, scorer, gateway.LeftItems, gateway.RightItems);
        if (best is null) return null;

        gateway.Equip(best, slot, character);
        return best.ItemRosterElement.EquipmentElement.GetModifiedItemName()?.ToString();
    }

    /// <summary>Drop cached item stats. Call when inventory contents change.</summary>
    public void InvalidateCaches()
    {
        _weightedScorer.InvalidateCache();
    }
}
