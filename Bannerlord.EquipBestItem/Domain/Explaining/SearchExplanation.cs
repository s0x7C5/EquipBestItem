using System.Collections.Generic;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Explaining;

public enum SearchMode
{
    Weights,
    Priority,
    Effectiveness
}

public enum ExplanationKind
{
    /// <summary>A found item beats the equipped one; <see cref="SearchExplanation.Factors" /> say why.</summary>
    Upgrade,

    /// <summary>The found item fills a slot that was empty.</summary>
    FirstItem,

    /// <summary>Nothing passed the search that clearly beats the equipped item.</summary>
    NothingBetter,

    /// <summary>The slot is excluded from searching (locked).</summary>
    SlotLocked
}

public enum FactorRole
{
    /// <summary>Priority mode: the rank that broke the tie.</summary>
    Decides,

    /// <summary>The found item is ahead on this stat.</summary>
    Advantage,

    /// <summary>The found item is behind on this stat.</summary>
    Disadvantage,

    /// <summary>Priority mode: a rank the two items tied on, above the deciding one.</summary>
    Tie
}

/// <summary>One stat's part in the comparison, with the raw values and their in-class percentiles.</summary>
public readonly struct ExplanationFactor
{
    public ExplanationFactor(
        ItemParam param, float foundValue, float currentValue,
        int foundPercentile, int currentPercentile, FactorRole role)
    {
        Param = param;
        FoundValue = foundValue;
        CurrentValue = currentValue;
        FoundPercentile = foundPercentile;
        CurrentPercentile = currentPercentile;
        Role = role;
    }

    public ItemParam Param { get; }
    public float FoundValue { get; }
    public float CurrentValue { get; }

    /// <summary>0..100 within the item's class, or -1 when unranked (e.g. priority ties).</summary>
    public int FoundPercentile { get; }
    public int CurrentPercentile { get; }

    public FactorRole Role { get; }
}

/// <summary>
///     Pure-data account of why the search picked (or didn't pick) an item for
///     a slot — produced by <see cref="ItemExplainer" /> from the same scoring
///     the search uses, so it can never disagree with the actual result. The
///     UI turns it into localized prose; an AI layer could narrate the same
///     facts.
/// </summary>
public sealed class SearchExplanation
{
    public EquipmentIndex Slot { get; set; }

    public SearchMode Mode { get; set; }

    public ExplanationKind Kind { get; set; }

    public string FoundItemName { get; set; } = "";

    public string CurrentItemName { get; set; } = "";

    /// <summary>Weights/Effectiveness: the two total scores. Unused in priority mode.</summary>
    public float FoundScore { get; set; }

    public float CurrentScore { get; set; }

    /// <summary>Per-stat breakdown, most important first.</summary>
    public List<ExplanationFactor> Factors { get; } = new();

    /// <summary>A stat the player could weigh more / rank higher to change the pick, if any.</summary>
    public ItemParam? TweakParam { get; set; }
}
