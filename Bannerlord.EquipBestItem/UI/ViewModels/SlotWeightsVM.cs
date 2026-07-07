using System;
using System.Collections.Generic;
using Bannerlord.EquipBestItem.Domain;
using Bannerlord.EquipBestItem.Profiles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.EquipBestItem.UI.ViewModels;

/// <summary>Popup for tuning per-slot search weights and pinning a weapon class.</summary>
public sealed class SlotWeightsVM : ViewModel
{
    private static readonly ItemParam[] ArmorParams =
        { ItemParam.HeadArmor, ItemParam.BodyArmor, ItemParam.ArmArmor, ItemParam.LegArmor, ItemParam.Weight };

    private static readonly ItemParam[] HorseParams =
        { ItemParam.ChargeDamage, ItemParam.HitPoints, ItemParam.Maneuver, ItemParam.Speed };

    private static readonly ItemParam[] HarnessParams =
        { ItemParam.MountArmor, ItemParam.Weight };

    private static readonly ItemParam[] WeaponParams =
    {
        ItemParam.ThrustDamage, ItemParam.SwingDamage, ItemParam.ThrustSpeed, ItemParam.SwingSpeed,
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy, ItemParam.MaxAmmo,
        ItemParam.WeaponLength, ItemParam.Handling, ItemParam.HitPoints, ItemParam.Weight
    };

    private static readonly WeaponClass?[] WeaponClassChoices =
    {
        null,
        TaleWorlds.Core.WeaponClass.OneHandedSword, TaleWorlds.Core.WeaponClass.TwoHandedSword,
        TaleWorlds.Core.WeaponClass.OneHandedAxe, TaleWorlds.Core.WeaponClass.TwoHandedAxe,
        TaleWorlds.Core.WeaponClass.Mace, TaleWorlds.Core.WeaponClass.TwoHandedMace,
        TaleWorlds.Core.WeaponClass.Dagger,
        TaleWorlds.Core.WeaponClass.OneHandedPolearm, TaleWorlds.Core.WeaponClass.TwoHandedPolearm,
        TaleWorlds.Core.WeaponClass.Bow, TaleWorlds.Core.WeaponClass.Crossbow,
        TaleWorlds.Core.WeaponClass.Arrow, TaleWorlds.Core.WeaponClass.Bolt,
        TaleWorlds.Core.WeaponClass.Javelin, TaleWorlds.Core.WeaponClass.ThrowingAxe,
        TaleWorlds.Core.WeaponClass.ThrowingKnife,
        TaleWorlds.Core.WeaponClass.SmallShield, TaleWorlds.Core.WeaponClass.LargeShield
    };

    private readonly ProfileService _profiles;

    private CharacterObject? _character;
    private Equipment? _equipment;
    private EquipmentIndex _slot;
    private int _weaponClassIndex;
    private bool _isVisible;
    private string _headerText = "";
    private MBBindingList<ParamRowVM> _rows = new();

    public SlotWeightsVM(ProfileService profiles)
    {
        _profiles = profiles;
    }

    [DataSourceProperty]
    public string ResetButtonText { get; } =
        new TextObject("{=EbiReset}Reset").ToString();

    [DataSourceProperty]
    public string CloseButtonText { get; } =
        new TextObject("{=EbiClose}Close").ToString();

    [DataSourceProperty]
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (value == _isVisible) return;
            _isVisible = value;
            OnPropertyChangedWithValue(value);
        }
    }

    [DataSourceProperty]
    public string HeaderText
    {
        get => _headerText;
        set
        {
            if (value == _headerText) return;
            _headerText = value;
            OnPropertyChangedWithValue(value);
        }
    }

    [DataSourceProperty]
    public MBBindingList<ParamRowVM> Rows
    {
        get => _rows;
        set
        {
            if (ReferenceEquals(value, _rows)) return;
            _rows = value;
            OnPropertyChangedWithValue(value);
        }
    }

    [DataSourceProperty]
    public bool IsWeaponSlot => _slot >= EquipmentIndex.Weapon0 && _slot <= EquipmentIndex.Weapon3;

    [DataSourceProperty]
    public string WeaponClassText => WeaponClassChoices[_weaponClassIndex] is { } weaponClass
        ? weaponClass.ToString()
        : new TextObject("{=EbiClassAsEquipped}As equipped").ToString();

    public void Open(CharacterObject character, Equipment equipment, EquipmentIndex slot)
    {
        _character = character;
        _equipment = equipment;
        _slot = slot;

        var query = _profiles.GetQuery(character, equipment, slot);
        _weaponClassIndex = Math.Max(0, Array.IndexOf(WeaponClassChoices, query.WeaponClass));

        var rows = new MBBindingList<ParamRowVM>();
        foreach (var param in GetParamsForSlot(slot))
            rows.Add(new ParamRowVM(param, GetParamName(param), query.Weights[param], PersistWeights));
        Rows = rows;

        HeaderText = new TextObject("{=EbiWeightsHeader}Search weights: {SLOT}")
            .SetTextVariable("SLOT", GetSlotName(slot)).ToString();

        OnPropertyChanged(nameof(IsWeaponSlot));
        OnPropertyChanged(nameof(WeaponClassText));
        IsVisible = true;
    }

    public void ExecuteClose()
    {
        IsVisible = false;
        _profiles.Save();
    }

    public void ExecuteReset()
    {
        if (_character is null || _equipment is null) return;

        _profiles.ResetSlot(_character, _equipment, _slot);
        Open(_character, _equipment, _slot);
    }

    public void ExecutePreviousWeaponClass() => CycleWeaponClass(-1);

    public void ExecuteNextWeaponClass() => CycleWeaponClass(1);

    private void CycleWeaponClass(int step)
    {
        if (_character is null || _equipment is null) return;

        var count = WeaponClassChoices.Length;
        _weaponClassIndex = (_weaponClassIndex + step + count) % count;

        _profiles.SetWeaponClass(_character, _equipment, _slot, WeaponClassChoices[_weaponClassIndex]);
        OnPropertyChanged(nameof(WeaponClassText));
    }

    private void PersistWeights()
    {
        if (_character is null || _equipment is null) return;

        var weights = new ParamWeights();
        foreach (var row in _rows)
            weights[row.Param] = row.Value;

        _profiles.SetWeights(_character, _equipment, _slot, weights);
    }

    private static IReadOnlyList<ItemParam> GetParamsForSlot(EquipmentIndex slot)
    {
        if (slot >= EquipmentIndex.Weapon0 && slot <= EquipmentIndex.Weapon3) return WeaponParams;
        if (slot == EquipmentIndex.Horse) return HorseParams;
        if (slot == EquipmentIndex.HorseHarness) return HarnessParams;
        return ArmorParams;
    }

    private static string GetParamName(ItemParam param) => param switch
    {
        ItemParam.HeadArmor => new TextObject("{=EbiParamHeadArmor}Head armor").ToString(),
        ItemParam.BodyArmor => new TextObject("{=EbiParamBodyArmor}Body armor").ToString(),
        ItemParam.ArmArmor => new TextObject("{=EbiParamArmArmor}Arm armor").ToString(),
        ItemParam.LegArmor => new TextObject("{=EbiParamLegArmor}Leg armor").ToString(),
        ItemParam.MountArmor => new TextObject("{=EbiParamMountArmor}Mount armor").ToString(),
        ItemParam.ChargeDamage => new TextObject("{=EbiParamChargeDamage}Charge damage").ToString(),
        ItemParam.HitPoints => new TextObject("{=EbiParamHitPoints}Hit points").ToString(),
        ItemParam.Maneuver => new TextObject("{=EbiParamManeuver}Maneuver").ToString(),
        ItemParam.Speed => new TextObject("{=EbiParamSpeed}Speed").ToString(),
        ItemParam.MaxAmmo => new TextObject("{=EbiParamMaxAmmo}Ammo / durability").ToString(),
        ItemParam.ThrustSpeed => new TextObject("{=EbiParamThrustSpeed}Thrust speed").ToString(),
        ItemParam.SwingSpeed => new TextObject("{=EbiParamSwingSpeed}Swing speed").ToString(),
        ItemParam.MissileSpeed => new TextObject("{=EbiParamMissileSpeed}Missile speed").ToString(),
        ItemParam.MissileDamage => new TextObject("{=EbiParamMissileDamage}Missile damage").ToString(),
        ItemParam.WeaponLength => new TextObject("{=EbiParamWeaponLength}Length").ToString(),
        ItemParam.ThrustDamage => new TextObject("{=EbiParamThrustDamage}Thrust damage").ToString(),
        ItemParam.SwingDamage => new TextObject("{=EbiParamSwingDamage}Swing damage").ToString(),
        ItemParam.Accuracy => new TextObject("{=EbiParamAccuracy}Accuracy").ToString(),
        ItemParam.Handling => new TextObject("{=EbiParamHandling}Handling").ToString(),
        ItemParam.Weight => new TextObject("{=EbiParamWeight}Weight").ToString(),
        _ => param.ToString()
    };

    private static string GetSlotName(EquipmentIndex slot) => slot switch
    {
        EquipmentIndex.Weapon0 => new TextObject("{=EbiSlotWeapon1}Weapon 1").ToString(),
        EquipmentIndex.Weapon1 => new TextObject("{=EbiSlotWeapon2}Weapon 2").ToString(),
        EquipmentIndex.Weapon2 => new TextObject("{=EbiSlotWeapon3}Weapon 3").ToString(),
        EquipmentIndex.Weapon3 => new TextObject("{=EbiSlotWeapon4}Weapon 4").ToString(),
        EquipmentIndex.Head => new TextObject("{=EbiSlotHead}Helmet").ToString(),
        EquipmentIndex.Body => new TextObject("{=EbiSlotBody}Body armor").ToString(),
        EquipmentIndex.Leg => new TextObject("{=EbiSlotLeg}Boots").ToString(),
        EquipmentIndex.Gloves => new TextObject("{=EbiSlotGloves}Gloves").ToString(),
        EquipmentIndex.Cape => new TextObject("{=EbiSlotCape}Cape").ToString(),
        EquipmentIndex.Horse => new TextObject("{=EbiSlotHorse}Mount").ToString(),
        EquipmentIndex.HorseHarness => new TextObject("{=EbiSlotHarness}Harness").ToString(),
        _ => slot.ToString()
    };
}
