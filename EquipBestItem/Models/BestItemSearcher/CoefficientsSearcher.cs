using System;
using EquipBestItem.Extensions;
using EquipBestItem.Models.Entities;
using EquipBestItem.Models.Enums;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;

namespace EquipBestItem.Models.BestItemSearcher;

public class CoefficientsSearcher : SearcherBase
{
    private readonly CharacterCoefficientsRepository _repository;

    public CoefficientsSearcher(CharacterCoefficientsRepository repository) : base(repository)
    {
        _repository = repository;
    }

    public override string Name => ModTexts.Coefficients;

    public override float GetItemValue(EquipmentElement equipmentElement, SearcherContext context)
    {
        var itemObject = equipmentElement.Item;
        var characterName = context.Character.Name.ToString();

        var coefficients = context.IsInWarSet
            ? _repository.Read(characterName).WarCoefficients[(int) context.EquipmentIndex]
            : _repository.Read(characterName).CivilCoefficients[(int) context.EquipmentIndex];

        if (itemObject.HasArmorComponent) return GetComponentValue(0, ItemTypes.Armor);
        if (itemObject.HasHorseComponent) return GetComponentValue(0, ItemTypes.Horse);
        if (!itemObject.HasWeaponComponent) return 0;

        var pw = equipmentElement.Item.PrimaryWeapon;

        return GetComponentValue(0, ItemTypes.GetParamsByWeaponClass(pw.WeaponClass));

        float GetComponentValue(int indexUsage = 0, params ItemParams[] itemParams)
        {
            float sumCoefficients = 0;
            float value = 0;

            foreach (var param in itemParams)
            {
                var coefficientValue = coefficients.GetCoefficientValue(param);

                if (coefficientValue == 0) continue;

                sumCoefficients += coefficientValue;

                value += equipmentElement.GetModifiedValue(param, indexUsage) * coefficientValue;
            }

            return sumCoefficients > 0 ? value / sumCoefficients : 0;
        }
    }

    protected override bool IsItemNotValid(SPItemVM item, SearcherContext context)
    {
        var index = context.EquipmentIndex;
        var character = context.Character;
        var characterName = context.Character.Name.ToString();
        var equipment = context.IsInWarSet ? character.FirstBattleEquipment : character.FirstCivilianEquipment;
        var coefficients = context.IsInWarSet
            ? _repository.Read(characterName).WarCoefficients[(int) index]
            : _repository.Read(characterName).CivilCoefficients[(int) index];
        
        var itemPrimaryWeapon = item.ItemRosterElement.EquipmentElement.Item?.PrimaryWeapon;
        var currentPrimaryWeapon = equipment[index].Item?.PrimaryWeapon;

        // If the selected weapon class is not defined, we look at the weapon class from the slot,
        // otherwise by the selected
        if (coefficients.WeaponClass == WeaponClass.Undefined)
        {
            // If the classes do not match, we skip
            if (itemPrimaryWeapon?.WeaponClass != currentPrimaryWeapon?.WeaponClass) return true;

            // Additional filter for short and long bows
            if (currentPrimaryWeapon?.WeaponClass == WeaponClass.Bow &&
                itemPrimaryWeapon?.ItemUsage != currentPrimaryWeapon.ItemUsage) return true;

            var currentWeaponComponents = item.ItemRosterElement.EquipmentElement.Item?.Weapons;
            var itemWeaponComponents = equipment[index].Item?.Weapons;

            // If they differ in the number of components skip
            if (itemWeaponComponents?.Count != currentWeaponComponents?.Count)
                return true;

            // If they have the same number of components, then we enumerate each one and compare by class
            for (var i = 0; i < itemWeaponComponents?.Count; i++)
                if (currentWeaponComponents?[i].ItemUsage != itemWeaponComponents?[i].ItemUsage)
                    return true;
        }
        else
        {
            if (coefficients.WeaponClass != itemPrimaryWeapon?.WeaponClass) return true;
        }
        
        if (IsShieldNotValid()) return true;

        if (itemPrimaryWeapon?.WeaponClass == WeaponClass.Banner) return true;
        
        return false;
        
        bool IsShieldNotValid()
        {
            if (!IsShield(coefficients.WeaponClass)) return false;

            // Exclude shields from the search if the shield is already on
            for (var i = EquipmentIndex.Weapon0; i <= EquipmentIndex.ExtraWeaponSlot; i++)
                if (IsShield(equipment[i].Item?.PrimaryWeapon?.WeaponClass)) return true;
                    
            return false;

            bool IsShield(WeaponClass? weaponClass)
            {
                return weaponClass is WeaponClass.SmallShield or WeaponClass.LargeShield;
            }
        }
    }
    
    // Checking the compliance of the slot's weapon class, if a weapon class other than WeaponClass.Undefined
    public override bool IsSlotItemNotValid(EquipmentElement item, SearcherContext context)
    {
        var index = context.EquipmentIndex;
        var characterName = context.Character.Name.ToString();
        var coefficients = context.IsInWarSet
            ? _repository.Read(characterName).WarCoefficients[(int) index]
            : _repository.Read(characterName).CivilCoefficients[(int) index];
                
        return item.Item?.PrimaryWeapon?.WeaponClass != coefficients.WeaponClass &&
               coefficients.WeaponClass != WeaponClass.Undefined;
    }
}