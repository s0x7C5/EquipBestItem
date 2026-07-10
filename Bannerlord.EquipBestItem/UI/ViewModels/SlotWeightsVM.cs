using System;
using System.Collections.Generic;
using Bannerlord.EquipBestItem.Domain;
using Bannerlord.EquipBestItem.Profiles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
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

    // Shown when no weapon class is pinned ("as equipped" can be anything).
    private static readonly ItemParam[] WeaponParams =
    {
        ItemParam.ThrustDamage, ItemParam.SwingDamage, ItemParam.ThrustSpeed, ItemParam.SwingSpeed,
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy, ItemParam.MaxAmmo,
        ItemParam.WeaponLength, ItemParam.Handling, ItemParam.HitPoints, ItemParam.Weight
    };

    private static readonly ItemParam[] MeleeParams =
    {
        ItemParam.ThrustDamage, ItemParam.SwingDamage, ItemParam.ThrustSpeed, ItemParam.SwingSpeed,
        ItemParam.WeaponLength, ItemParam.Handling, ItemParam.Weight
    };

    private static readonly ItemParam[] BowParams =
    {
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy,
        ItemParam.ThrustSpeed, ItemParam.Weight
    };

    private static readonly ItemParam[] CrossbowParams =
    {
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy,
        ItemParam.ThrustSpeed, ItemParam.MaxAmmo, ItemParam.Weight
    };

    private static readonly ItemParam[] ThrownParams =
    {
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy,
        ItemParam.WeaponLength, ItemParam.MaxAmmo, ItemParam.Weight
    };

    private static readonly ItemParam[] AmmoParams =
    {
        ItemParam.MissileDamage, ItemParam.MaxAmmo, ItemParam.Weight
    };

    private static readonly ItemParam[] ShieldParams =
    {
        ItemParam.HitPoints, ItemParam.BodyArmor, ItemParam.ThrustSpeed,
        ItemParam.WeaponLength, ItemParam.Weight
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
    private readonly Action _onChanged;

    private CharacterObject? _character;
    private Equipment? _equipment;
    private EquipmentIndex _slot;
    private int _weaponClassIndex;
    private bool _isVisible;
    private string _headerText = "";
    private MBBindingList<ParamRowVM> _rows = new();

    public SlotWeightsVM(ProfileService profiles, Action onChanged)
    {
        _profiles = profiles;
        _onChanged = onChanged;
    }

    [DataSourceProperty]
    public string DefaultButtonText { get; } =
        new TextObject("{=ebi_default}Default").ToString();

    [DataSourceProperty]
    public string LockButtonText { get; } =
        new TextObject("{=ebi_lock}Lock").ToString();

    [DataSourceProperty]
    public HintViewModel DefaultButtonHint { get; } =
        new(new TextObject("{=ebi_hint_default}Reset to default values"));

    [DataSourceProperty]
    public HintViewModel LockButtonHint { get; } =
        new(new TextObject("{=ebi_hint_lock}Disable search for this slot"));

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
        ? GameTexts.FindText("str_inventory_weapon", ((int)weaponClass).ToString()).ToString()
        : new TextObject("{=EbiClassAsEquipped}As equipped").ToString();

    public void Open(CharacterObject character, Equipment equipment, EquipmentIndex slot)
    {
        _character = character;
        _equipment = equipment;
        _slot = slot;

        var query = _profiles.GetQuery(character, equipment, slot);
        _weaponClassIndex = Math.Max(0, Array.IndexOf(WeaponClassChoices, query.WeaponClass));

        RebuildRows();

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
        _onChanged();
    }

    /// <summary>Zeroes every weight: an all-zero slot is excluded from searching.</summary>
    public void ExecuteLock()
    {
        if (_character is null || _equipment is null) return;

        _profiles.SetWeights(_character, _equipment, _slot, new ParamWeights());
        RebuildRows();
        _onChanged();
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

        // Each weapon class exposes its own parameter set.
        RebuildRows();
        _onChanged();
    }

    private void RebuildRows()
    {
        if (_character is null || _equipment is null) return;

        var query = _profiles.GetQuery(_character, _equipment, _slot);

        var rows = new MBBindingList<ParamRowVM>();
        foreach (var param in GetVisibleParams())
            rows.Add(new ParamRowVM(param, GetParamName(param), query.Weights[param], PersistWeights));
        Rows = rows;
    }

    /// <summary>
    ///     Merges the visible sliders into the stored weights instead of
    ///     replacing them, so values set under another weapon class survive
    ///     switching back and forth.
    /// </summary>
    private void PersistWeights()
    {
        if (_character is null || _equipment is null) return;

        var weights = _profiles.GetQuery(_character, _equipment, _slot).Weights;
        foreach (var row in _rows)
            weights[row.Param] = row.Value;

        _profiles.SetWeights(_character, _equipment, _slot, weights);

        // Live preview: the slot buttons re-search as the sliders move.
        _onChanged();
    }

    private IReadOnlyList<ItemParam> GetVisibleParams()
    {
        if (IsWeaponSlot)
            return WeaponClassChoices[_weaponClassIndex] is { } weaponClass
                ? GetParamsForWeaponClass(weaponClass)
                : WeaponParams;

        if (_slot == EquipmentIndex.Horse) return HorseParams;
        if (_slot == EquipmentIndex.HorseHarness) return HarnessParams;
        return ArmorParams;
    }

    private static ItemParam[] GetParamsForWeaponClass(WeaponClass weaponClass) => weaponClass switch
    {
        TaleWorlds.Core.WeaponClass.Bow => BowParams,
        TaleWorlds.Core.WeaponClass.Crossbow => CrossbowParams,
        TaleWorlds.Core.WeaponClass.Arrow or TaleWorlds.Core.WeaponClass.Bolt => AmmoParams,
        TaleWorlds.Core.WeaponClass.Javelin or TaleWorlds.Core.WeaponClass.ThrowingAxe
            or TaleWorlds.Core.WeaponClass.ThrowingKnife => ThrownParams,
        TaleWorlds.Core.WeaponClass.SmallShield or TaleWorlds.Core.WeaponClass.LargeShield => ShieldParams,
        _ => MeleeParams
    };

    /// <summary>
    ///     The game's own item stat strings (their values end with ": ", hence
    ///     the trim), so every language the game ships is supported for free.
    /// </summary>
    private static string GetParamName(ItemParam param) => (param switch
    {
        ItemParam.HeadArmor => new TextObject("{=EUzxzL9s}Head Armor: "),
        ItemParam.BodyArmor => new TextObject("{=bLWyjOdS}Body Armor: "),
        ItemParam.ArmArmor => new TextObject("{=cf61cce254c7dca65be9bebac7fb9bf5}Arm Armor: "),
        ItemParam.LegArmor => new TextObject("{=U8VHRdwF}Leg Armor: "),
        ItemParam.MountArmor => new TextObject("{=bLWyjOdS}Body Armor: "),
        ItemParam.ChargeDamage => new TextObject("{=c7638a0869219ae845de0f660fd57a9d}Charge Damage: "),
        ItemParam.HitPoints => new TextObject("{=aCkzVUCR}Hit Points: "),
        ItemParam.Maneuver => new TextObject("{=3025020b83b218707499f0de3135ed0a}Maneuver: "),
        ItemParam.Speed => new TextObject("{=74dc1908cb0b990e80fb977b5a0ef10d}Speed: "),
        ItemParam.MaxAmmo => new TextObject("{=05fdfc6e238429753ef282f2ce97c1f8}Stack Amount: "),
        ItemParam.ThrustSpeed => new TextObject("{=VPYazFVH}Thrust Speed: "),
        ItemParam.SwingSpeed => new TextObject("{=nfQhamAF}Swing Speed: "),
        ItemParam.MissileSpeed => new TextObject("{=YukbQgHJ}Missile Speed: "),
        ItemParam.MissileDamage => new TextObject("{=c9c5dfed2ca6bcb7a73d905004c97b23}Damage: "),
        ItemParam.WeaponLength => new TextObject("{=XUtiwiYP}Length: "),
        ItemParam.ThrustDamage => new TextObject("{=7sUhWG0E}Thrust Damage: "),
        ItemParam.SwingDamage => new TextObject("{=fMmlUHyz}Swing Damage: "),
        ItemParam.Accuracy => new TextObject("{=xEWwbGVK}Accuracy: "),
        ItemParam.Handling => new TextObject("{=YOSEIvyf}Handling: "),
        ItemParam.Weight => new TextObject("{=YvwQL9aa}Weight: "),
        _ => new TextObject(param.ToString())
    }).ToString().TrimEnd(':', ' ');

    private static string GetSlotName(EquipmentIndex slot)
    {
        if (slot >= EquipmentIndex.Weapon0 && slot <= EquipmentIndex.Weapon3)
        {
            var number = slot - EquipmentIndex.Weapon0 + 1;
            return $"{new TextObject("{=2RIyK1bp}Weapons")} {number}";
        }

        return (slot switch
        {
            EquipmentIndex.Head => new TextObject("{=bg6x6Hbv}Helm"),
            EquipmentIndex.Body => new TextObject("{=ahiBhAqU}Armor"),
            EquipmentIndex.Leg => new TextObject("{=Xx9EbSwG}Boot"),
            EquipmentIndex.Gloves => new TextObject("{=3ZRTekjS}Glove"),
            EquipmentIndex.Cape => new TextObject("{=QAv3upYr}Cloak"),
            EquipmentIndex.Horse => new TextObject("{=mountnoun}Mount"),
            EquipmentIndex.HorseHarness => new TextObject("{=0GZ19XHb}Harness"),
            _ => new TextObject(slot.ToString())
        }).ToString();
    }
}
