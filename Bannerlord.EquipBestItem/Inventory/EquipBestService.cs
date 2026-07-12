using System;
using Bannerlord.EquipBestItem.Domain;
using Bannerlord.EquipBestItem.Domain.Scoring;
using Bannerlord.EquipBestItem.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

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
    private readonly PriorityItemComparer _priorityComparer;
    private readonly ItemStatCache _statCache;
    private readonly ModSettings _settings;

    public EquipBestService(
        BestItemFinder finder,
        WeightedItemScorer weightedScorer,
        EffectivenessItemScorer effectivenessScorer,
        PriorityItemComparer priorityComparer,
        ItemStatCache statCache,
        ModSettings settings)
    {
        _finder = finder;
        _weightedScorer = weightedScorer;
        _effectivenessScorer = effectivenessScorer;
        _priorityComparer = priorityComparer;
        _statCache = statCache;
        _settings = settings;
    }

    /// <summary>Search only — used to preview the best item on the slot buttons.</summary>
    public SPItemVM? FindBest(InventoryGateway gateway, ItemQuery query, EquipmentIndex slot)
    {
        return FindBest(gateway, query, slot, gateway.CurrentCharacter, gateway.ActiveEquipment);
    }

    /// <summary>Search for an arbitrary hero — used by "equip all characters".</summary>
    /// <param name="extraCandidates">
    ///     Items not (yet) present in the panels, e.g. ones displaced by
    ///     earlier steps of a planned batch. Always searched regardless of the
    ///     panel toggles: they belong to the player.
    /// </param>
    public SPItemVM? FindBest(
        InventoryGateway gateway, ItemQuery query, EquipmentIndex slot,
        CharacterObject? character, Equipment? equipment,
        Func<SPItemVM, bool>? exclude = null,
        MBBindingList<SPItemVM>? extraCandidates = null)
    {
        if (character is null || equipment is null) return null;

        var context = new SearchContext(character, equipment, slot, query);

        // The player chooses which panels participate: searching the left
        // (merchant/loot) side means "buy out everything better".
        var leftItems = _settings.SearchLeftPanel ? gateway.LeftItems : null;
        var rightItems = _settings.SearchRightPanel ? gateway.RightItems : null;

        // Priority mode ranks stat by stat; AI directives with explicit
        // weights still go through the weighted scorer.
        if (_settings.UsePriority && !query.HasExplicitWeights)
        {
            if (query.Priorities is { Count: 0 }) return null; // slot disabled by the player

            return _finder.FindBest(context, _priorityComparer, exclude, leftItems, rightItems, extraCandidates);
        }

        var useWeights = !_settings.UseEffectiveness || query.HasExplicitWeights;
        if (useWeights && query.Weights.IsEmpty) return null; // slot disabled by the player

        var scorer = useWeights ? (IItemScorer)_weightedScorer : _effectivenessScorer;

        return _finder.FindBest(context, scorer, exclude, leftItems, rightItems, extraCandidates);
    }

    /// <summary>Equips a previously found item into the slot.</summary>
    public void Equip(InventoryGateway gateway, SPItemVM item, EquipmentIndex slot)
    {
        var character = gateway.CurrentCharacter;
        if (character is null) return;

        gateway.Equip(item, slot, character);
    }

    /// <returns>The display name of the equipped item, or null when nothing better was found.</returns>
    public string? TryEquipBest(InventoryGateway gateway, ItemQuery query, EquipmentIndex slot)
    {
        return TryEquipBest(gateway, query, slot, gateway.CurrentCharacter, gateway.ActiveEquipment);
    }

    /// <returns>The display name of the equipped item, or null when nothing better was found.</returns>
    public string? TryEquipBest(
        InventoryGateway gateway, ItemQuery query, EquipmentIndex slot,
        CharacterObject? character, Equipment? equipment)
    {
        var best = FindBest(gateway, query, slot, character, equipment);
        if (best is null) return null;

        gateway.Equip(best, slot, character!);
        return best.ItemRosterElement.EquipmentElement.GetModifiedItemName()?.ToString();
    }

    /// <summary>Drop cached item stats. Call when inventory contents change.</summary>
    public void InvalidateCaches()
    {
        _statCache.Invalidate();
    }
}
