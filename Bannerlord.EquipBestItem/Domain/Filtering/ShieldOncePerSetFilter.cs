using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Filtering;

/// <summary>Rejects shield candidates when another weapon slot already holds a shield.</summary>
public sealed class ShieldOncePerSetFilter : IItemFilter
{
    public bool IsSatisfiedBy(SPItemVM item, in SearchContext context)
    {
        if (item.ItemRosterElement.EquipmentElement.Item?.PrimaryWeapon?.IsShield != true) return true;

        for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.Weapon3; slot++)
        {
            if (slot == context.Slot) continue;
            if (context.Equipment[slot].Item?.PrimaryWeapon?.IsShield == true) return false;
        }

        return true;
    }
}
