using System;
using System.Collections.Generic;
using Bannerlord.EquipBestItem.Domain.Filtering;
using Bannerlord.EquipBestItem.Domain.Scoring;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Explaining;

/// <summary>
///     Explains, deterministically, why the search picked (or didn't pick) an
///     item for a slot. It reuses the exact scorers the search uses — the
///     weighted contribution breakdown, the priority decision — so the account
///     always matches the real ranking. Produces pure data; the UI phrases it.
/// </summary>
public sealed class ItemExplainer
{
    private const float ContributionEpsilon = 0.0005f;
    private const float StatEpsilon = 0.001f;
    private const int MaxFactors = 5;

    private readonly ItemStatCache _stats;
    private readonly ItemStatPercentiles _percentiles;
    private readonly WeightedItemScorer _weighted;
    private readonly PriorityItemComparer _priority;
    private readonly IReadOnlyList<IItemFilter> _filters;

    public ItemExplainer(
        ItemStatCache stats, ItemStatPercentiles percentiles,
        WeightedItemScorer weighted, PriorityItemComparer priority,
        IReadOnlyList<IItemFilter> filters)
    {
        _stats = stats;
        _percentiles = percentiles;
        _weighted = weighted;
        _priority = priority;
        _filters = filters;
    }

    public SearchExplanation Explain(
        CharacterObject character, Equipment equipment, EquipmentIndex slot,
        ItemQuery query, SearchMode mode, SPItemVM? found)
    {
        var context = new SearchContext(character, equipment, slot, query);
        var current = equipment[slot];
        var explanation = new SearchExplanation
        {
            Slot = slot,
            Mode = mode,
            CurrentItemName = ItemName(current)
        };

        if (found is null)
        {
            explanation.Kind = IsSlotLocked(query, mode) ? ExplanationKind.SlotLocked : ExplanationKind.NothingBetter;
            return explanation;
        }

        var foundElement = found.ItemRosterElement.EquipmentElement;
        explanation.FoundItemName = ItemName(foundElement);

        if (current.IsEmpty || current.Item is null)
        {
            explanation.Kind = ExplanationKind.FirstItem;
            return explanation;
        }

        explanation.Kind = ExplanationKind.Upgrade;
        switch (mode)
        {
            case SearchMode.Priority:
                ExplainPriority(explanation, context, foundElement, current);
                break;
            case SearchMode.Effectiveness:
                explanation.FoundScore = foundElement.Item?.Effectiveness ?? 0f;
                explanation.CurrentScore = current.Item?.Effectiveness ?? 0f;
                break;
            default:
                ExplainWeights(explanation, context, foundElement, current);
                break;
        }

        return explanation;
    }

    /// <summary>
    ///     A "why not this named item" account: whether the candidate was
    ///     filtered out (and by what), is itself the pick, or qualifies but is
    ///     beaten — with the same breakdown as the positive case. The winner
    ///     is the recommended item, or the equipped one when nothing was
    ///     recommended.
    /// </summary>
    public SearchExplanation ExplainRejection(
        CharacterObject character, Equipment equipment, EquipmentIndex slot,
        ItemQuery query, SearchMode mode, SPItemVM candidate, SPItemVM? found, string queriedName)
    {
        var context = new SearchContext(character, equipment, slot, query);
        var current = equipment[slot];
        var candidateElement = candidate.ItemRosterElement.EquipmentElement;
        var explanation = new SearchExplanation
        {
            Slot = slot,
            Mode = mode,
            QueriedName = queriedName,
            CurrentItemName = ItemName(current),
            FoundItemName = ItemName(candidateElement)
        };

        // Rejected before scoring? Report which gate.
        foreach (var filter in _filters)
            if (!filter.IsSatisfiedBy(candidate, context))
            {
                explanation.Kind = ExplanationKind.NamedFilteredOut;
                explanation.FilterReason = ReasonFor(filter, candidate, query);
                return explanation;
            }

        if (found is not null && ReferenceEquals(found.ItemRosterElement.EquipmentElement.Item, candidateElement.Item))
        {
            explanation.Kind = ExplanationKind.NamedIsBest;
            return explanation;
        }

        // The item qualifies. Compare the winner (the pick, or the equipped
        // item when nothing was picked) against it.
        if (found is not null)
        {
            explanation.Kind = ExplanationKind.NamedLoses;
            explanation.FoundItemName = ItemName(found.ItemRosterElement.EquipmentElement);
            explanation.CurrentItemName = ItemName(candidateElement);
            CompareInto(explanation, context, mode, found.ItemRosterElement.EquipmentElement, candidateElement);
            return explanation;
        }

        // Nothing was recommended: the equipped item held its place.
        if (mode == SearchMode.Weights &&
            _weighted.Score(candidateElement, context) > _weighted.Score(current, context))
        {
            // Scores higher, but not by the margin the upgrade guard demands.
            // CurrentItemName stays the equipped item: the message reads
            // "<named> scores a bit higher than <equipped>".
            explanation.Kind = ExplanationKind.NamedMarginal;
            explanation.FoundItemName = ItemName(candidateElement);
            CompareInto(explanation, context, mode, candidateElement, current);
        }
        else
        {
            // Here the named item takes the "loser" seat of the template:
            // "<equipped> is picked over <named>".
            explanation.Kind = ExplanationKind.NamedLoses;
            explanation.FoundItemName = ItemName(current);
            explanation.CurrentItemName = ItemName(candidateElement);
            CompareInto(explanation, context, mode, current, candidateElement);
        }

        return explanation;
    }

    private void CompareInto(
        SearchExplanation explanation, in SearchContext context, SearchMode mode,
        EquipmentElement better, EquipmentElement worse)
    {
        switch (mode)
        {
            case SearchMode.Priority:
                ExplainPriority(explanation, context, better, worse);
                break;
            case SearchMode.Effectiveness:
                explanation.FoundScore = better.Item?.Effectiveness ?? 0f;
                explanation.CurrentScore = worse.Item?.Effectiveness ?? 0f;
                break;
            default:
                ExplainWeights(explanation, context, better, worse);
                break;
        }
    }

    private static RejectionReason ReasonFor(IItemFilter filter, SPItemVM item, ItemQuery query)
    {
        switch (filter)
        {
            case SlotFilter: return RejectionReason.WrongTypeForSlot;
            case WeaponMatchFilter: return RejectionReason.WrongWeaponClass;
            case ShieldOncePerSetFilter: return RejectionReason.ShieldAlreadyEquipped;
            case EquippableFilter: return RejectionReason.NotEquippable;
            case QueryConstraintFilter:
                var element = item.ItemRosterElement.EquipmentElement;
                return query.MaxItemWeight > 0f && element.Item?.Weight > query.MaxItemWeight
                    ? RejectionReason.OverWeightLimit
                    : RejectionReason.WrongCulture;
            default: return RejectionReason.None;
        }
    }

    private static bool IsSlotLocked(ItemQuery query, SearchMode mode) => mode switch
    {
        SearchMode.Priority => query.Priorities is { Count: 0 },
        SearchMode.Weights => query.Weights.IsEmpty,
        _ => false
    };

    private void ExplainWeights(
        SearchExplanation explanation, in SearchContext context,
        EquipmentElement found, EquipmentElement current)
    {
        var harness = context.Equipment[EquipmentIndex.HorseHarness];
        var foundStats = _stats.GetStats(found, harness);
        var currentStats = _stats.GetStats(current, harness);

        var foundContrib = new float[ItemParams.Count];
        var currentContrib = new float[ItemParams.Count];
        _weighted.Breakdown(found, context, foundContrib);
        _weighted.Breakdown(current, context, currentContrib);

        var weights = context.Query.Weights.Raw;
        var scored = new List<(ExplanationFactor Factor, float Impact)>();
        var bestTweakGap = 0;

        for (var i = 0; i < ItemParams.Count; i++)
        {
            explanation.FoundScore += foundContrib[i];
            explanation.CurrentScore += currentContrib[i];

            var foundPct = Percentile(found.Item, i, foundStats[i]);
            var currentPct = Percentile(current.Item, i, currentStats[i]);

            if (weights[i] != 0f)
            {
                var delta = foundContrib[i] - currentContrib[i];
                if (Math.Abs(delta) < ContributionEpsilon) continue;

                var role = delta > 0f ? FactorRole.Advantage : FactorRole.Disadvantage;
                scored.Add((new ExplanationFactor(
                    (ItemParam)i, foundStats[i], currentStats[i], foundPct, currentPct, role), Math.Abs(delta)));
            }

            // Tweak: the stat where the current item leads by the most —
            // valuing it more (its weight) could flip the pick.
            var gap = currentPct - foundPct;
            if (gap > bestTweakGap && currentStats[i] > foundStats[i] + StatEpsilon)
            {
                bestTweakGap = gap;
                explanation.TweakParam = (ItemParam)i;
            }
        }

        scored.Sort((a, b) => b.Impact.CompareTo(a.Impact));
        for (var i = 0; i < scored.Count && i < MaxFactors; i++)
            explanation.Factors.Add(scored[i].Factor);
    }

    private void ExplainPriority(
        SearchExplanation explanation, in SearchContext context,
        EquipmentElement found, EquipmentElement current)
    {
        var decision = _priority.Explain(found, current, context);
        if (decision.DecidingRank < 0) return;

        var harness = context.Equipment[EquipmentIndex.HorseHarness];
        var foundStats = _stats.GetStats(found, harness);
        var currentStats = _stats.GetStats(current, harness);

        // The tie ranks above the deciding one, then the deciding rank itself.
        for (var rank = 0; rank <= decision.DecidingRank; rank++)
        {
            var role = rank == decision.DecidingRank ? FactorRole.Decides : FactorRole.Tie;
            foreach (var param in decision.Order[rank])
            {
                var index = (int)param;
                explanation.Factors.Add(new ExplanationFactor(
                    param, foundStats[index], currentStats[index],
                    Percentile(found.Item, index, foundStats[index]),
                    Percentile(current.Item, index, currentStats[index]), role));
            }
        }

        // Tweak: the first stat below the deciding rank where the found item is
        // behind — moving it up would let it matter.
        for (var rank = decision.DecidingRank + 1; rank < decision.Order.Count; rank++)
        foreach (var param in decision.Order[rank])
            if (currentStats[(int)param] > foundStats[(int)param] + StatEpsilon)
            {
                explanation.TweakParam = param;
                return;
            }
    }

    private int Percentile(ItemObject? item, int param, float value) =>
        (int)Math.Round(_percentiles.Normalize(item, param, value) * 100f);

    private static string ItemName(EquipmentElement element) =>
        element.Item is null ? "" : element.GetModifiedItemName()?.ToString() ?? element.Item.Name?.ToString() ?? "";
}
