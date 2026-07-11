using System;
using System.Collections.Generic;
using System.Globalization;
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

    /// <summary>Every weapon class a slot can pin (the popup's selector, minus "as equipped").</summary>
    internal static IEnumerable<WeaponClass> PinnableWeaponClasses
    {
        get
        {
            foreach (var choice in WeaponClassChoices)
                if (choice is { } weaponClass)
                    yield return weaponClass;
        }
    }

    private readonly ProfileService _profiles;
    private readonly Action _onChanged;

    /// <summary>Selectable culture restrictions; null = any culture.</summary>
    private static readonly string?[] CultureChoices =
        { null, "empire", "sturgia", "aserai", "vlandia", "battania", "khuzait" };

    private const float MaxItemWeightCap = 40f;

    private CharacterObject? _character;
    private Equipment? _equipment;
    private EquipmentIndex _slot;
    private int _weaponClassIndex;
    private int _cultureIndex;
    private float _maxItemWeight;
    private bool _isVisible;
    private bool _isOnDefault;
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
    public string MakeDefaultButtonText { get; } =
        new TextObject("{=EbiMakeDefault}Make default").ToString();

    [DataSourceProperty]
    public string OnDefaultText { get; } =
        new TextObject("{=EbiOnDefault}Default values").ToString();

    /// <summary>True while the hero follows the defaults for this slot (no override of their own).</summary>
    [DataSourceProperty]
    public bool IsOnDefault
    {
        get => _isOnDefault;
        set
        {
            if (value == _isOnDefault) return;
            _isOnDefault = value;
            OnPropertyChangedWithValue(value);
        }
    }

    [DataSourceProperty]
    public HintViewModel MakeDefaultButtonHint { get; } = new(new TextObject(
        "{=EbiHintMakeDefault}Save this slot's filter as the default for every hero without their own settings"));

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

    /// <summary>The selected culture restriction, or "any culture".</summary>
    [DataSourceProperty]
    public string CultureText => CultureChoices[_cultureIndex] is { } cultureId
        ? GetCultureName(cultureId)
        : new TextObject("{=EbiAnyCulture}Any culture").ToString();

    [DataSourceProperty]
    public string MaxWeightLabel { get; } =
        new TextObject("{=EbiMaxItemWeight}Weight limit").ToString();

    /// <summary>Skip items heavier than this, kg; 0 disables the cap.</summary>
    [DataSourceProperty]
    public float MaxItemWeight
    {
        get => _maxItemWeight;
        set
        {
            if (Math.Abs(value - _maxItemWeight) < 0.05f) return;
            _maxItemWeight = value;
            OnPropertyChangedWithValue(value);
            OnPropertyChanged(nameof(MaxItemWeightText));
            PersistConstraints();
        }
    }

    [DataSourceProperty]
    public string MaxItemWeightText =>
        _maxItemWeight > 0f ? _maxItemWeight.ToString("0.0", CultureInfo.InvariantCulture) : "—";

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
        // The game's tooltip uses the enum NAME as the text variation.
        ? GameTexts.FindText("str_inventory_weapon", weaponClass.ToString()).ToString()
        : new TextObject("{=EbiClassAsEquipped}As equipped").ToString();

    public void Open(CharacterObject character, Equipment equipment, EquipmentIndex slot)
    {
        _character = character;
        _equipment = equipment;
        _slot = slot;

        var query = _profiles.GetQuery(character, equipment, slot);
        _weaponClassIndex = Math.Max(0, Array.IndexOf(WeaponClassChoices, query.WeaponClass));
        _cultureIndex = Math.Max(0, Array.IndexOf(CultureChoices, query.CultureId));
        _maxItemWeight = query.MaxItemWeight;

        RebuildRows();

        HeaderText = GetSlotName(slot);

        OnPropertyChanged(nameof(IsWeaponSlot));
        OnPropertyChanged(nameof(WeaponClassText));
        OnPropertyChanged(nameof(CultureText));
        OnPropertyChanged(nameof(MaxItemWeight));
        OnPropertyChanged(nameof(MaxItemWeightText));
        RefreshDefaultMarker();
        IsVisible = true;
    }

    private void RefreshDefaultMarker()
    {
        if (_character is null || _equipment is null) return;

        IsOnDefault = !_profiles.HasOverride(_character, _equipment, _slot);
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

    public void ExecuteMakeDefault()
    {
        if (_character is null || _equipment is null) return;

        _profiles.SaveAsDefault(_character, _equipment, _slot);
        Open(_character, _equipment, _slot);
        _onChanged();
    }

    /// <summary>Zeroes every weight: an all-zero slot is excluded from searching.</summary>
    public void ExecuteLock()
    {
        if (_character is null || _equipment is null) return;

        _profiles.SetWeights(_character, _equipment, _slot, new ParamWeights());
        RebuildRows();
        RefreshDefaultMarker();
        _onChanged();
    }

    public void ExecutePreviousWeaponClass() => CycleWeaponClass(-1);

    public void ExecuteNextWeaponClass() => CycleWeaponClass(1);

    public void ExecutePreviousCulture() => CycleCulture(-1);

    public void ExecuteNextCulture() => CycleCulture(1);

    private void CycleCulture(int step)
    {
        var count = CultureChoices.Length;
        _cultureIndex = (_cultureIndex + step + count) % count;
        OnPropertyChanged(nameof(CultureText));
        PersistConstraints();
    }

    private void PersistConstraints()
    {
        if (_character is null || _equipment is null) return;

        _profiles.SetConstraints(_character, _equipment, _slot, CultureChoices[_cultureIndex], _maxItemWeight);
        RefreshDefaultMarker();

        // Live preview, same as the weight sliders.
        _onChanged();
    }

    private void CycleWeaponClass(int step)
    {
        if (_character is null || _equipment is null) return;

        var count = WeaponClassChoices.Length;
        _weaponClassIndex = (_weaponClassIndex + step + count) % count;

        _profiles.SetWeaponClass(_character, _equipment, _slot, WeaponClassChoices[_weaponClassIndex]);
        OnPropertyChanged(nameof(WeaponClassText));
        RefreshDefaultMarker();

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

        UpdateShares();
    }

    /// <summary>
    ///     Influence share of each visible weight: value / Σ|values|. The
    ///     scorer computes Σ(w·v) / Σ|w|, so this is exactly the fraction of
    ///     the search's attention the parameter gets; the sign shows the
    ///     direction. Absolute shares always add up to 100% and nothing
    ///     explodes when positive and negative weights cancel out (dividing
    ///     by the signed sum does: -1, -0.95, +1, +1 gave ±2000%).
    /// </summary>
    private void UpdateShares()
    {
        var absSum = 0f;
        foreach (var row in _rows) absSum += Math.Abs(row.Value);
        foreach (var row in _rows) row.UpdateShare(absSum);
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
        UpdateShares();
        RefreshDefaultMarker();

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
    internal static string GetParamName(ItemParam param) => (param switch
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

    /// <summary>The game's localized culture name, or the raw id when unknown.</summary>
    internal static string GetCultureName(string cultureId)
    {
        var culture = TaleWorlds.ObjectSystem.MBObjectManager.Instance?.GetObject<CultureObject>(cultureId);
        return culture?.Name?.ToString() ?? cultureId;
    }

    internal static string GetSlotName(EquipmentIndex slot)
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
