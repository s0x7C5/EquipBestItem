using System.Collections.Generic;
using Bannerlord.EquipBestItem.Domain;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Ai;

/// <summary>The structured result of interpreting a player request.</summary>
public sealed class InterpretedPlan
{
    public InterpretedPlan(IReadOnlyList<EquipDirective> directives, string explanation, string target)
    {
        Directives = directives;
        Explanation = explanation;
        Target = target;
    }

    public IReadOnlyList<EquipDirective> Directives { get; }

    /// <summary>Human-readable summary in the player's language.</summary>
    public string Explanation { get; }

    /// <summary>
    ///     Whose filters the plan edits: "current" (default), "others" (every
    ///     party hero except the main one), "all", or an exact hero name.
    /// </summary>
    public string Target { get; }
}

/// <summary>One "search this slot with this query" instruction.</summary>
public sealed class EquipDirective
{
    public EquipDirective(EquipmentIndex slot, ItemQuery query)
    {
        Slot = slot;
        Query = query;
    }

    public EquipmentIndex Slot { get; }

    public ItemQuery Query { get; }
}
