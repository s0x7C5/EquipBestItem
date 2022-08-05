using EquipBestItem.Models.Entities;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;

namespace EquipBestItem.Models.BestItemSearcher;

public interface ISearcher
{
    string Name { get; }
    
    float GetItemValue(EquipmentElement equipmentElement, SearcherContext context);
    
    bool IsValidItem(SPItemVM item, SearcherContext context);
    
    bool IsSlotItemNotValid(EquipmentElement equipmentElement, SearcherContext context);
}