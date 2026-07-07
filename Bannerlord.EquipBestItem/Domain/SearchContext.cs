using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain;

/// <summary>Everything a filter or scorer needs to judge one candidate.</summary>
public readonly struct SearchContext
{
    public SearchContext(CharacterObject character, Equipment equipment, EquipmentIndex slot, ItemQuery query)
    {
        Character = character;
        Equipment = equipment;
        Slot = slot;
        Query = query;
    }

    public CharacterObject Character { get; }

    /// <summary>The equipment set currently shown in the inventory (battle, civilian or stealth).</summary>
    public Equipment Equipment { get; }

    public EquipmentIndex Slot { get; }

    public ItemQuery Query { get; }
}
