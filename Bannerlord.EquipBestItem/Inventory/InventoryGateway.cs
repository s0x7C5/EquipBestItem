using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// <summary>
    ///     SPInventoryVM.AfterTransfer only FLAGS the character model as dirty
    ///     (_isCharacterEquipmentDirty); the actual repaint lives in the
    ///     private RefreshInformationValues, which native UI equip handlers
    ///     call themselves. Our transfers bypass those handlers, so without
    ///     this call the paperdoll keeps showing the old gear.
    /// </summary>
    private static readonly Action<SPInventoryVM>? RefreshInformationValues =
        typeof(SPInventoryVM)
                .GetMethod("RefreshInformationValues", BindingFlags.Instance | BindingFlags.NonPublic)?
                .CreateDelegate(typeof(Action<SPInventoryVM>))
            as Action<SPInventoryVM>;

    private readonly SPInventoryVM _vm;

    public InventoryGateway(SPInventoryVM vm)
    {
        _vm = vm;
    }

    public Equipment? ActiveEquipment => GetEquipmentFor(CurrentCharacter);

    /// <summary>The character's equipment set matching the shown mode (battle/civilian/stealth).</summary>
    public Equipment? GetEquipmentFor(CharacterObject? character) =>
        (SPInventoryVM.EquipmentModes)_vm.EquipmentMode switch
        {
            SPInventoryVM.EquipmentModes.Civilian => character?.FirstCivilianEquipment,
            SPInventoryVM.EquipmentModes.Stealth => character?.FirstStealthEquipment,
            _ => character?.FirstBattleEquipment
        };

    /// <summary>Party heroes whose equipment the player may change.</summary>
    public IEnumerable<CharacterObject> GetEquippableHeroes()
    {
        var roster = ActiveInventoryLogic?.RightMemberRoster?.GetTroopRoster();
        if (roster is null) yield break;

        for (var i = 0; i < roster.Count; i++)
        {
            var character = roster[i].Character;
            if (character is { IsHero: true } && character.HeroObject.CanHeroEquipmentBeChanged())
                yield return character;
        }
    }

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

    /// <summary>The item VM currently shown in an equipped slot.</summary>
    public SPItemVM? GetEquippedItemVM(EquipmentIndex slot) => slot switch
    {
        EquipmentIndex.Weapon0 => _vm.CharacterWeapon1Slot,
        EquipmentIndex.Weapon1 => _vm.CharacterWeapon2Slot,
        EquipmentIndex.Weapon2 => _vm.CharacterWeapon3Slot,
        EquipmentIndex.Weapon3 => _vm.CharacterWeapon4Slot,
        EquipmentIndex.Head => _vm.CharacterHelmSlot,
        EquipmentIndex.Body => _vm.CharacterTorsoSlot,
        EquipmentIndex.Leg => _vm.CharacterBootSlot,
        EquipmentIndex.Gloves => _vm.CharacterGloveSlot,
        EquipmentIndex.Cape => _vm.CharacterCloakSlot,
        EquipmentIndex.Horse => _vm.CharacterMountSlot,
        EquipmentIndex.HorseHarness => _vm.CharacterMountArmorSlot,
        _ => null
    };

    private static InventoryLogic? ActiveInventoryLogic =>
        InventoryScreenHelper.GetActiveInventoryState()?.InventoryLogic;

    public void Equip(SPItemVM item, EquipmentIndex slot, CharacterObject character)
    {
        var logic = ActiveInventoryLogic;
        if (logic is null) return;

        logic.AddTransferCommand(BuildEquipCommand(item, slot, character));

        // Equipping the last piece of a stack leaves a zero-count row in the
        // item list; the native screen removes those explicitly.
        _vm.ExecuteRemoveZeroCounts();
        RefreshInformationValues?.Invoke(_vm);
    }

    /// <summary>
    ///     Executes many equips as one transfer batch, the way the native
    ///     "buy all" does: the game refreshes the item lists and fires
    ///     AfterTransfer once for the whole batch instead of per item, which
    ///     keeps "equip all characters" from freezing large inventories.
    /// </summary>
    public void EquipBatch(IReadOnlyList<TransferCommand> commands)
    {
        if (commands.Count == 0) return;

        var logic = ActiveInventoryLogic;
        if (logic is null) return;

        logic.AddTransferCommands(commands);
        _vm.ExecuteRemoveZeroCounts();
        RefreshInformationValues?.Invoke(_vm);
    }

    public TransferCommand BuildEquipCommand(SPItemVM item, EquipmentIndex slot, CharacterObject character) =>
        TransferCommand.Transfer(
            1,
            item.InventorySide,
            GetEquipmentSide(),
            item.ItemRosterElement,
            item.ItemType,
            slot,
            character);

    /// <summary>
    ///     Wraps an item displaced by a planned batch step as a candidate for
    ///     the remaining steps. By the time a later command executes, the swap
    ///     that released this item has already put it into the player inventory.
    /// </summary>
    public SPItemVM? CreateReleasedItemVM(EquipmentElement element)
    {
        var logic = ActiveInventoryLogic;
        if (logic is null || element.IsEmpty || element.Item is null) return null;

        return new SPItemVM(
            logic,
            CharacterObject.PlayerCharacter?.IsFemale ?? false,
            true,
            InventoryScreenHelper.GetActiveInventoryState()?.InventoryMode ?? default,
            new ItemRosterElement(element, 1),
            InventoryLogic.InventorySide.PlayerInventory,
            0,
            null);
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
