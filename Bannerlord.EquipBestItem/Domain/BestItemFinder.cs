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
        var hasBaseline = CurrentSetsBaseline(context);
        var baseline = context.Equipment[context.Slot];
        var bestScore = hasBaseline ? scorer.Score(baseline, context) : 0f;
        SPItemVM? best = null;

        foreach (var list in candidateLists)
        {
            if (list is null) continue;

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item is null || exclude?.Invoke(item) == true) continue;
                if (!PassesAllFilters(item, context)) continue;

                var element = item.ItemRosterElement.EquipmentElement;

                // The scorer decides what counts as a worthwhile upgrade over
                // the equipped item (margin/dominance); ranking among the
                // candidates themselves stays a plain argmax.
                if (hasBaseline && !scorer.BeatsCurrent(element, baseline, context)) continue;

                var score = scorer.Score(element, context);
                if (score <= bestScore) continue;

                bestScore = score;
                best = item;
            }
        }

        return best;
    }

    /// <summary>
    ///     Comparer-based search for modes without a scalar score: the winner
    ///     must strictly beat the current slot item (when it sets a baseline)
    ///     and every other candidate. An empty or dismissed slot means any
    ///     valid candidate qualifies.
    /// </summary>
    public SPItemVM? FindBest(
        in SearchContext context,
        IItemComparer comparer,
        Func<SPItemVM, bool>? exclude,
        params MBBindingList<SPItemVM>?[] candidateLists)
    {
        var hasBaseline = CurrentSetsBaseline(context);
        var baseline = context.Equipment[context.Slot];
        SPItemVM? best = null;

        foreach (var list in candidateLists)
        {
            if (list is null) continue;

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item is null || exclude?.Invoke(item) == true) continue;
                if (!PassesAllFilters(item, context)) continue;

                var element = item.ItemRosterElement.EquipmentElement;
                if (hasBaseline && comparer.Compare(element, baseline, context) <= 0) continue;
                if (best is not null &&
                    comparer.Compare(element, best.ItemRosterElement.EquipmentElement, context) <= 0) continue;

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

    /// <summary>
    ///     A pinned weapon category the current item does not match means the
    ///     player wants a replacement, so the current item sets no baseline.
    /// </summary>
    private static bool CurrentSetsBaseline(in SearchContext context)
    {
        var current = context.Equipment[context.Slot];
        if (current.IsEmpty || current.Item is null) return false;

        if (context.Query.WeaponCategory is { } pinned &&
            (current.Item.PrimaryWeapon is not { } currentWeapon || !pinned.Matches(currentWeapon)))
            return false;

        return true;
    }
}
