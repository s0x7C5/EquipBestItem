using Bannerlord.EquipBestItem.Domain;
using Bannerlord.EquipBestItem.Domain.Scoring;
using Bannerlord.EquipBestItem.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Inventory;

/// <summary>
///     Application-level "find the best item for this slot and equip it".
///     The player picks the scoring method in settings.json: parameter weights
///     (the mod's main mode) or the game's built-in Effectiveness. Weights
///     explicitly requested by an AI directive win over the setting, and a
///     slot whose weights were zeroed out is excluded from weight searches.
/// </summary>
public sealed class EquipBestService
{
    private readonly BestItemFinder _finder;
    private readonly WeightedItemScorer _weightedScorer;
    private readonly EffectivenessItemScorer _effectivenessScorer;
    private readonly ModSettings _settings;

    public EquipBestService(
        BestItemFinder finder,
        WeightedItemScorer weightedScorer,
        EffectivenessItemScorer effectivenessScorer,
        ModSettings settings)
    {
        _finder = finder;
        _weightedScorer = weightedScorer;
        _effectivenessScorer = effectivenessScorer;
        _settings = settings;
    }

    /// <returns>The display name of the equipped item, or null when nothing better was found.</returns>
    public string? TryEquipBest(InventoryGateway gateway, ItemQuery query, EquipmentIndex slot)
    {
        var character = gateway.CurrentCharacter;
        var equipment = gateway.ActiveEquipment;
        if (character is null || equipment is null) return null;

        var useWeights = !_settings.UseEffectiveness || query.HasExplicitWeights;
        if (useWeights && query.Weights.IsEmpty) return null; // slot disabled by the player

        var context = new SearchContext(character, equipment, slot, query);
        var scorer = useWeights ? (IItemScorer)_weightedScorer : _effectivenessScorer;

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
