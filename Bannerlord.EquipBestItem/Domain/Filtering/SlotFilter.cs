using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Filtering;

/// <summary>Keeps only items that physically fit the target slot.</summary>
public sealed class SlotFilter : IItemFilter
{
    public bool IsSatisfiedBy(SPItemVM item, in SearchContext context)
    {
        var slot = context.Slot;

        // Banners are not searchable equipment.
        if (slot == EquipmentIndex.ExtraWeaponSlot) return false;

        if (slot >= EquipmentIndex.Weapon0 && slot <= EquipmentIndex.Weapon3)
            return item.ItemType >= EquipmentIndex.Weapon0 && item.ItemType <= EquipmentIndex.Weapon3;

        if (slot == EquipmentIndex.HorseHarness)
            return item.ItemType == EquipmentIndex.HorseHarness && IsHarnessCompatible(item, context);

        return item.ItemType == slot;
    }

    private static bool IsHarnessCompatible(SPItemVM item, in SearchContext context)
    {
        var horse = context.Equipment[EquipmentIndex.Horse];
        if (horse.IsEmpty) return false;

        return horse.Item?.HorseComponent?.Monster?.FamilyType ==
               item.ItemRosterElement.EquipmentElement.Item?.ArmorComponent?.FamilyType;
    }
}
