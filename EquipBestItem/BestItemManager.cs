using System;
using System.Linq;
using EquipBestItem.Models;
using EquipBestItem.Models.BestItemSearcher;
using EquipBestItem.Models.Entities;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace EquipBestItem;

public class BestItemManager
{
    private readonly ISearcher _searcher;

    public BestItemManager(CharacterCoefficientsRepository repository)
    {
        _searcher = Helper.GetEnumerableOfType<SearcherBase>(repository).First(s => s.Name == MCMSettings.Instance?.SearchMethod.SelectedValue);
    }

    public SPItemVM? GetBestItem(SearcherContext context, params MBBindingList<SPItemVM>?[] itemsLists)
    {
        SPItemVM? bestItem = null;

        var index = context.EquipmentIndex;
        var character = context.Character;
        var equipment = context.IsInWarSet ? character.FirstBattleEquipment : character.FirstCivilianEquipment;

        try
        {
            var bestItemValue = equipment[index].IsEmpty || _searcher.IsSlotItemNotValid(equipment[index], context)
                ? 0f : _searcher.GetItemValue(equipment[index], context);

            var validItems = itemsLists
                .Where(items => items is not null)
                .SelectMany(items => items)
                .Where(item => _searcher.IsValidItem(item, context));
            
            foreach (var item in validItems)
            {
                var itemValue = _searcher.GetItemValue(item.ItemRosterElement.EquipmentElement, context);
                
                if (bestItemValue >= itemValue) continue;
                
                bestItem = item;
                bestItemValue = itemValue;
            }
        }
        catch (Exception e)
        {
            Helper.ShowMessage($"{e.Message}", Colors.Red);
        }

        return bestItem;
    }

    public static void UnequipItem(SearcherContext context)
    {
        var equipmentIndex = context.EquipmentIndex;
        var character = context.Character;
        var isInWarSet = context.IsInWarSet;
        var inventoryLogic = InventoryManager.InventoryLogic;
        
        var equipment = isInWarSet ? character.FirstBattleEquipment : character.FirstCivilianEquipment;

        if (equipment[equipmentIndex].IsEmpty) return;

        var unequipCommand = TransferCommand.Transfer(
            1,
            InventoryLogic.InventorySide.Equipment,
            InventoryLogic.InventorySide.PlayerInventory,
            new ItemRosterElement(equipment[equipmentIndex], 1),
            equipmentIndex,
            EquipmentIndex.None,
            character,
            !isInWarSet);

        inventoryLogic.AddTransferCommand(unequipCommand);
    }

    public static void EquipItem(SearcherContext context, SPItemVM? item)
    {
        if (item is null) return;

        var equipmentIndex = context.EquipmentIndex;
        var character = context.Character;
        var isInWarSet = context.IsInWarSet;
        var inventoryLogic = InventoryManager.InventoryLogic;
        
        var equipCommand = TransferCommand.Transfer(
            1,
            item.InventorySide,
            InventoryLogic.InventorySide.Equipment,
            new ItemRosterElement(item.ItemRosterElement.EquipmentElement, 1),
            EquipmentIndex.None,
            equipmentIndex,
            character,
            !isInWarSet);

        inventoryLogic.AddTransferCommand(equipCommand);
    }
}