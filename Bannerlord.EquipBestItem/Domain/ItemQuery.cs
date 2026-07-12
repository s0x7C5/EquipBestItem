using System.Collections.Generic;

namespace Bannerlord.EquipBestItem.Domain;

/// <summary>
///     A structured "what do I want in this slot" request. Both the manual
///     weight sliders and the AI interpreter produce this same shape, so the
///     search pipeline does not care where a query came from.
/// </summary>
public sealed class ItemQuery
{
    public ItemQuery(ParamWeights weights)
    {
        Weights = weights;
    }

    public ParamWeights Weights { get; }

    /// <summary>Restrict the search to a specific weapon category (weapon slots only).</summary>
    public WeaponCategory? WeaponCategory { get; set; }

    /// <summary>
    ///     Stat-priority order for the priority search mode: an ordered list
    ///     of groups, most important first; stats inside one group are
    ///     equal-rank and judged together. Null = the slot's default order;
    ///     an empty list = the slot is excluded from priority searches (the
    ///     priority-mode "Lock").
    /// </summary>
    public IReadOnlyList<IReadOnlyList<ItemParam>>? Priorities { get; set; }

    /// <summary>Skip items heavier than this. Non-positive value disables the constraint.</summary>
    public float MaxItemWeight { get; set; }

    /// <summary>Restrict the search to items of a culture (e.g. "empire"). Null disables the constraint.</summary>
    public string? CultureId { get; set; }

    /// <summary>
    ///     True when the weights were explicitly requested (e.g. spelled out by
    ///     an AI directive) and must be honored even if the player's search
    ///     method setting is "effectiveness".
    /// </summary>
    public bool HasExplicitWeights { get; set; }
}
