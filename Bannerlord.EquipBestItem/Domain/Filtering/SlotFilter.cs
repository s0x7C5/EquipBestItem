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

        if (slot == EquipmentIndex.Horse)
            return item.ItemType == EquipmentIndex.Horse && IsSameMountFamily(item, context);

        if (slot == EquipmentIndex.HorseHarness)
            return item.ItemType == EquipmentIndex.HorseHarness && IsHarnessCompatible(item, context);

        return item.ItemType == slot;
    }

    /// <summary>
    ///     Only offer mounts of the same family as the equipped one: a horse is
    ///     replaced by a horse, a camel by a camel. An empty slot accepts any mount.
    /// </summary>
    private static bool IsSameMountFamily(SPItemVM item, in SearchContext context)
    {
        var current = context.Equipment[EquipmentIndex.Horse];
        if (current.IsEmpty) return true;

        var currentFamily = current.Item?.HorseComponent?.Monster?.FamilyType;
        var candidateFamily = item.ItemRosterElement.EquipmentElement.Item?.HorseComponent?.Monster?.FamilyType;

        return currentFamily is not null && currentFamily == candidateFamily;
    }

    /// <summary>
    ///     Same family check the game runs after every transfer (a mismatched
    ///     harness gets auto-unequipped natively): horse harness must not fit
    ///     camels and vice versa. Missing component data counts as a mismatch.
    /// </summary>
    private static bool IsHarnessCompatible(SPItemVM item, in SearchContext context)
    {
        var horse = context.Equipment[EquipmentIndex.Horse];
        if (horse.IsEmpty) return false;

        var mountFamily = horse.Item?.HorseComponent?.Monster?.FamilyType;
        var harnessFamily = item.ItemRosterElement.EquipmentElement.Item?.ArmorComponent?.FamilyType;

        return mountFamily is not null && harnessFamily is not null && mountFamily == harnessFamily;
    }
}
