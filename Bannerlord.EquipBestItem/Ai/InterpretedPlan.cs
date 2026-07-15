using System.Collections.Generic;
using Bannerlord.EquipBestItem.Domain;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Ai;

/// <summary>The structured result of interpreting a player request.</summary>
public sealed class InterpretedPlan
{
    public InterpretedPlan(
        IReadOnlyList<EquipDirective> directives, string explanation, string target,
        string answer = "", EquipmentIndex? explainSlot = null, string explainItem = "")
    {
        Directives = directives;
        Explanation = explanation;
        Target = target;
        Answer = answer;
        ExplainSlot = explainSlot;
        ExplainItem = explainItem;
    }

    public IReadOnlyList<EquipDirective> Directives { get; }

    /// <summary>Human-readable summary in the player's language.</summary>
    public string Explanation { get; }

    /// <summary>
    ///     A reply to a "why/how" question (grounded in the recommendation
    ///     facts), when the request was a question rather than a change. When
    ///     set with no directives, nothing is applied — the answer is shown.
    /// </summary>
    public string Answer { get; }

    /// <summary>
    ///     Whose filters the plan edits: "current" (default), "others" (every
    ///     party hero except the main one), "all", or an exact hero name.
    /// </summary>
    public string Target { get; }

    /// <summary>A "why not X" query: the slot and the named item to explain. Applied by looking the item up.</summary>
    public EquipmentIndex? ExplainSlot { get; }

    public string ExplainItem { get; }
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
