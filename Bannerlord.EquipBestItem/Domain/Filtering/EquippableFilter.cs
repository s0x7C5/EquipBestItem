using Helpers;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace Bannerlord.EquipBestItem.Domain.Filtering;

/// <summary>Basic "can this character put this item on at all" checks.</summary>
public sealed class EquippableFilter : IItemFilter
{
    public bool IsSatisfiedBy(SPItemVM item, in SearchContext context)
    {
        if (!item.IsEquipableItem) return false;
        if (item.IsLocked) return false;
        if (item.ItemCount <= 0) return false;

        if (context.Equipment.IsCivilian && !item.IsCivilianItem) return false;
        if (context.Equipment.IsStealth && !item.IsStealthItem) return false;

        return CharacterHelper.CanUseItem(context.Character, item.ItemRosterElement.EquipmentElement);
    }
}
