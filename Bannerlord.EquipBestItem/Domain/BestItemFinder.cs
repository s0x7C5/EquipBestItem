using System;
using System.Collections.Generic;
using Bannerlord.EquipBestItem.Domain.Filtering;
using Bannerlord.EquipBestItem.Domain.Scoring;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem.Domain;

/// <summary>
///     Single-pass argmax over candidate lists: the best item strictly better
///     than what is currently in the slot, or null.
/// </summary>
public sealed class BestItemFinder
{
    private readonly IReadOnlyList<IItemFilter> _filters;

    public BestItemFinder(IReadOnlyList<IItemFilter> filters)
    {
        _filters = filters;
    }

    public SPItemVM? FindBest(
        in SearchContext context,
        IItemScorer scorer,
        params MBBindingList<SPItemVM>?[] candidateLists)
    {
        return FindBest(context, scorer, null, candidateLists);
    }

    /// <param name="exclude">
    ///     Transient rejection hook, e.g. items already claimed while planning
    ///     a batch equip for several characters.
    /// </param>
    public SPItemVM? FindBest(
        in SearchContext context,
        IItemScorer scorer,
        Func<SPItemVM, bool>? exclude,
        params MBBindingList<SPItemVM>?[] candidateLists)
    {
        var bestScore = GetCurrentSlotScore(context, scorer);
        SPItemVM? best = null;

        foreach (var list in candidateLists)
        {
            if (list is null) continue;

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item is null || exclude?.Invoke(item) == true) continue;
                if (!PassesAllFilters(item, context)) continue;

                var score = scorer.Score(item.ItemRosterElement.EquipmentElement, context);
                if (score <= bestScore) continue;

                bestScore = score;
                best = item;
            }
        }

        return best;
    }

    private bool PassesAllFilters(SPItemVM item, in SearchContext context)
    {
        for (var i = 0; i < _filters.Count; i++)
            if (!_filters[i].IsSatisfiedBy(item, context))
                return false;
        return true;
    }

    private static float GetCurrentSlotScore(in SearchContext context, IItemScorer scorer)
    {
        var current = context.Equipment[context.Slot];
        if (current.IsEmpty || current.Item is null) return 0f;

        // A pinned weapon class different from the current item means the player
        // wants a replacement, so the current item sets no baseline.
        if (context.Query.WeaponClass is { } pinnedClass &&
            current.Item.PrimaryWeapon?.WeaponClass != pinnedClass)
            return 0f;

        return scorer.Score(current, context);
    }
}
