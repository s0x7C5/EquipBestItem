using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem.Inventory;

/// <summary>
///     The only class that talks to the live inventory screen state: current
///     character, active equipment set, candidate item lists and transfer
///     commands. Everything above it works with plain domain types.
/// </summary>
public sealed class InventoryGateway
{
    private readonly SPInventoryVM _vm;

    public InventoryGateway(SPInventoryVM vm)
    {
        _vm = vm;
    }

    public Equipment? ActiveEquipment =>
        (SPInventoryVM.EquipmentModes)_vm.EquipmentMode switch
        {
            SPInventoryVM.EquipmentModes.Civilian => CurrentCharacter?.FirstCivilianEquipment,
            SPInventoryVM.EquipmentModes.Stealth => CurrentCharacter?.FirstStealthEquipment,
            _ => CurrentCharacter?.FirstBattleEquipment
        };

    public MBBindingList<SPItemVM>? LeftItems => _vm.LeftItemListVM;

    public MBBindingList<SPItemVM>? RightItems => _vm.RightItemListVM;

    /// <summary>The hero currently shown in the middle of the inventory screen.</summary>
    public CharacterObject? CurrentCharacter
    {
        get
        {
            var logic = ActiveInventoryLogic;
            if (logic is null) return null;

            return FindHeroByName(logic.RightMemberRoster, _vm.CurrentCharacterName)
                   ?? FindHeroByName(logic.LeftMemberRoster, _vm.CurrentCharacterName);
        }
    }

    /// <summary>The screen's transfer engine; the reliable change-event source.</summary>
    public InventoryLogic? Logic => ActiveInventoryLogic;

    private static InventoryLogic? ActiveInventoryLogic =>
        InventoryScreenHelper.GetActiveInventoryState()?.InventoryLogic;

    public void Equip(SPItemVM item, EquipmentIndex slot, CharacterObject character)
    {
        var logic = ActiveInventoryLogic;
        if (logic is null) return;

        var equipmentSide = GetEquipmentSide();

        var command = TransferCommand.Transfer(
            1,
            item.InventorySide,
            equipmentSide,
            item.ItemRosterElement,
            item.ItemType,
            slot,
            character);

        logic.AddTransferCommand(command);
    }

    private InventoryLogic.InventorySide GetEquipmentSide() =>
        (SPInventoryVM.EquipmentModes)_vm.EquipmentMode switch
        {
            SPInventoryVM.EquipmentModes.Civilian => InventoryLogic.InventorySide.CivilianEquipment,
            SPInventoryVM.EquipmentModes.Stealth => InventoryLogic.InventorySide.StealthEquipment,
            _ => InventoryLogic.InventorySide.BattleEquipment
        };

    private static CharacterObject? FindHeroByName(TroopRoster? roster, string? name)
    {
        if (roster is null || string.IsNullOrEmpty(name)) return null;

        var elements = roster.GetTroopRoster();
        for (var i = 0; i < elements.Count; i++)
        {
            var character = elements[i].Character;
            if (character is { IsHero: true } && character.Name.ToString() == name)
                return character;
        }

        return null;
    }
}
