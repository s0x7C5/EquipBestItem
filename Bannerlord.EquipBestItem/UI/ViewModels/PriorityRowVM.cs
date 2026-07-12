using System;
using System.Collections.Generic;
using Bannerlord.EquipBestItem.Domain;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem.UI.ViewModels;

/// <summary>
///     One draggable stat chip in the slot settings popup (priority search
///     mode). Dropping a chip between rows makes it its own priority rank;
///     dropping it onto a row links it with that row's stats as equal-rank.
/// </summary>
public sealed class PriorityChipVM : ViewModel
{
    public PriorityChipVM(ItemParam param, string name)
    {
        Param = param;
        Name = name;
    }

    public ItemParam Param { get; }

    [DataSourceProperty]
    public string Name { get; }
}

/// <summary>
///     One priority rank in the popup: a position number and the equal-rank
///     stat chips that share it. Receives chips dropped onto the row's center
///     band (linking); drops that miss the band land on the surrounding list
///     and reorder instead.
/// </summary>
public sealed class PriorityRowVM : ViewModel
{
    private readonly Action<PriorityChipVM, PriorityRowVM> _linkChip;

    /// <param name="group">The backing group; identity is used to find it after list mutations.</param>
    public PriorityRowVM(
        List<ItemParam> group, int index, IEnumerable<PriorityChipVM> chips,
        Action<PriorityChipVM, PriorityRowVM> linkChip)
    {
        Group = group;
        PositionText = (index + 1) + ".";
        Chips = new MBBindingList<PriorityChipVM>();
        foreach (var chip in chips) Chips.Add(chip);
        _linkChip = linkChip;
    }

    internal List<ItemParam> Group { get; }

    [DataSourceProperty]
    public string PositionText { get; }

    [DataSourceProperty]
    public MBBindingList<PriorityChipVM> Chips { get; }

    /// <summary>Gauntlet drop handler: a chip was dropped onto this row — make it equal-rank.</summary>
    public void ExecuteLinkChip(PriorityChipVM chip, int index)
    {
        _linkChip(chip, this);
    }
}
