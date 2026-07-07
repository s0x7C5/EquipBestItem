using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Filtering;

/// <summary>
///     For weapon slots: a candidate must either match the weapon class pinned
///     in the query, or be interchangeable with the currently equipped weapon
///     (same class, usage and component layout), so that "find best" never
///     silently swaps a bow for a mace.
/// </summary>
public sealed class WeaponMatchFilter : IItemFilter
{
    public bool IsSatisfiedBy(SPItemVM item, in SearchContext context)
    {
        var slot = context.Slot;
        if (slot < EquipmentIndex.Weapon0 || slot > EquipmentIndex.Weapon3) return true;

        var candidate = item.ItemRosterElement.EquipmentElement.Item;
        var candidateWeapon = candidate?.PrimaryWeapon;
        if (candidateWeapon is null) return false;
        if (candidateWeapon.WeaponClass == WeaponClass.Banner) return false;

        if (context.Query.WeaponClass is { } pinnedClass)
            return candidateWeapon.WeaponClass == pinnedClass;

        var current = context.Equipment[slot].Item;
        var currentWeapon = current?.PrimaryWeapon;
        if (currentWeapon is null) return false;

        if (candidateWeapon.WeaponClass != currentWeapon.WeaponClass) return false;

        // Long and short bows share a class but not an item usage.
        if (candidateWeapon.ItemUsage != currentWeapon.ItemUsage) return false;

        return HaveSameComponentLayout(candidate!, current!);
    }

    private static bool HaveSameComponentLayout(ItemObject candidate, ItemObject current)
    {
        var candidateComponents = candidate.Weapons;
        var currentComponents = current.Weapons;

        if (candidateComponents?.Count != currentComponents?.Count) return false;

        for (var i = 0; i < candidateComponents?.Count; i++)
            if (candidateComponents[i].ItemUsage != currentComponents![i].ItemUsage)
                return false;

        return true;
    }
}
