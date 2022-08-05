using EquipBestItem.Models.Entities;
using Helpers;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;

namespace EquipBestItem.Models.BestItemSearcher;

public abstract class SearcherBase : ISearcher
{
    protected SearcherBase(CharacterCoefficientsRepository repository)
    {
    }
    
    public abstract string Name { get; }
    
    public abstract float GetItemValue(EquipmentElement equipmentElement, SearcherContext context);

    // Additional embedded conditions
    protected abstract bool IsItemNotValid(SPItemVM item, SearcherContext context);

    public abstract bool IsSlotItemNotValid(EquipmentElement equipmentElement, SearcherContext context);
    
    public bool IsValidItem(SPItemVM item, SearcherContext context)
    {
        if (!context.IsInWarSet && !item.IsCivilianItem) return false;
                
        if (!item.IsEquipableItem) return false;
                
        if (item.IsLocked) return false;
                
        if (item.ItemCount == 0) return false;
                
        if (!CharacterHelper.CanUseItemBasedOnSkill(context.Character, item.ItemRosterElement.EquipmentElement))
            return false;

        if (IsHorseHarnessNotValid()) return false;
                   
        if (IsItemNotWeapon()) return item.ItemType == context.EquipmentIndex;

        if (IsItemNotValid(item, context)) return false;

        return item.ItemType <= context.EquipmentIndex;

        bool IsHorseHarnessNotValid()
        {
            var equipment = context.IsInWarSet 
                ? context.Character.FirstBattleEquipment 
                : context.Character.FirstCivilianEquipment;
            
            if (item.ItemType != EquipmentIndex.HorseHarness) return false;
                    
            // Disable the saddles search if there is no mount in the slot
            if (equipment[EquipmentIndex.Horse].IsEmpty && item.ItemType == EquipmentIndex.HorseHarness)
                return true;
                    
            // Exclude from the search saddles that are not suitable for mount
            if (equipment[EquipmentIndex.Horse].Item?.HorseComponent?.Monster.FamilyType !=
                item.ItemRosterElement.EquipmentElement.Item?.ArmorComponent?.FamilyType) return true;

            return false;
        }
                
        // Separating the search for weapons from other items
        bool IsItemNotWeapon()
        {
            return item.ItemType != EquipmentIndex.Weapon0 || context.EquipmentIndex > EquipmentIndex.Weapon4;
        }
    }
}