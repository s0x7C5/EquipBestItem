using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    // The "Speed" the game prints on bows, crossbows and shields is the SWING
    // speed (speed_rating in the item XML). Their thrust_speed and (for
    // ranged) weapon_length are hidden from the game's tooltip but filled and
    // varying in the data, so they stay tunable — just at 0 by default.
    private static readonly ItemParam[] BowParams =
    {
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy,
        ItemParam.SwingSpeed, ItemParam.ThrustSpeed, ItemParam.WeaponLength, ItemParam.Weight
    };

    private static readonly ItemParam[] CrossbowParams =
    {
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy,
        ItemParam.SwingSpeed, ItemParam.ThrustSpeed, ItemParam.WeaponLength,
        ItemParam.MaxAmmo, ItemParam.Weight
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
        ItemParam.HitPoints, ItemParam.BodyArmor, ItemParam.SwingSpeed,
        ItemParam.ThrustSpeed, ItemParam.WeaponLength, ItemParam.Weight
    };

    private static readonly WeaponCategory?[] WeaponCategoryChoices =
    {
        null,
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.OneHandedSword),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.TwoHandedSword),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.OneHandedAxe),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.TwoHandedAxe),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.Mace),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.TwoHandedMace),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.Dagger),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.OneHandedPolearm),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.TwoHandedPolearm),
        WeaponCategory.ShortBow, WeaponCategory.LongBow,
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.Crossbow),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.Arrow),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.Bolt),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.Javelin),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.ThrowingAxe),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.ThrowingKnife),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.SmallShield),
        WeaponCategory.Of(TaleWorlds.Core.WeaponClass.LargeShield)
    };

    /// <summary>Every weapon category a slot can pin (the popup's selector, minus "as equipped").</summary>
    internal static IEnumerable<WeaponCategory> PinnableWeaponCategories
    {
        get
        {
            foreach (var choice in WeaponCategoryChoices)
                if (choice is { } category)
                    yield return category;
        }
    }

    /// <summary>
    ///     The localized category name: the game's own weapon class string,
    ///     except the bow split, which is the mod's.
    /// </summary>
    internal static string GetCategoryName(WeaponCategory category)
    {
        if (category.Class != TaleWorlds.Core.WeaponClass.Bow)
            // The game's tooltip uses the enum NAME as the text variation.
            return GameTexts.FindText("str_inventory_weapon", category.Class.ToString()).ToString();

        return (category.IsLongBow
            ? new TextObject("{=EbiLongBow}Long Bow")
            : new TextObject("{=EbiShortBow}Short Bow")).ToString();
    }

    private readonly ProfileService _profiles;
    private readonly Settings.ModSettings _settings;
    private readonly Action _onChanged;
    private readonly Action<CharacterObject, Equipment, EquipmentIndex> _explain;

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
    private MBBindingList<PriorityRowVM> _priorityRows = new();
    private readonly List<List<ItemParam>> _priorityGroups = new();

    public SlotWeightsVM(
        ProfileService profiles, Settings.ModSettings settings, Action onChanged,
        Action<CharacterObject, Equipment, EquipmentIndex> explain)
    {
        _profiles = profiles;
        _settings = settings;
        _onChanged = onChanged;
        _explain = explain;
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
    public string ExplainButtonText { get; } =
        new TextObject("{=EbiExplain}Why this?").ToString();

    [DataSourceProperty]
    public HintViewModel ExplainButtonHint { get; } = new(new TextObject(
        "{=EbiExplainHint}Explain this slot's pick in the message log"));

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
    public MBBindingList<PriorityRowVM> PriorityRows
    {
        get => _priorityRows;
        set
        {
            if (ReferenceEquals(value, _priorityRows)) return;
            _priorityRows = value;
            OnPropertyChangedWithValue(value);
        }
    }

    /// <summary>Priority mode replaces the weight sliders with a reorderable stat list.</summary>
    [DataSourceProperty]
    public bool IsPriorityMode => _settings.UsePriority;

    [DataSourceProperty]
    public bool IsWeightsMode => !_settings.UsePriority && !_settings.UseEffectiveness;

    /// <summary>
    ///     Effectiveness mode ignores weights and priorities, so the popup
    ///     shows only what still applies: the weapon class pin, the culture
    ///     restriction and the weight cap.
    /// </summary>
    [DataSourceProperty]
    public bool IsEffectivenessMode => _settings.UseEffectiveness;

    /// <summary>Lock works by zeroing weights, which effectiveness searches ignore.</summary>
    [DataSourceProperty]
    public bool IsLockVisible => !_settings.UseEffectiveness;

    [DataSourceProperty]
    public string EffectivenessNoteText { get; } = new TextObject(
        "{=EbiEffectivenessNote}Items are ranked by the game's built-in Effectiveness score; stat weights do not apply.").ToString();

    [DataSourceProperty]
    public bool IsWeaponSlot => _slot >= EquipmentIndex.Weapon0 && _slot <= EquipmentIndex.Weapon3;

    [DataSourceProperty]
    public string WeaponClassText => WeaponCategoryChoices[_weaponClassIndex] is { } category
        ? GetCategoryName(category)
        : new TextObject("{=EbiClassAsEquipped}As equipped").ToString();

    public void Open(CharacterObject character, Equipment equipment, EquipmentIndex slot)
    {
        _character = character;
        _equipment = equipment;
        _slot = slot;

        var query = _profiles.GetQuery(character, equipment, slot);
        _weaponClassIndex = Math.Max(0, Array.IndexOf(WeaponCategoryChoices, query.WeaponCategory));
        _cultureIndex = Math.Max(0, Array.IndexOf(CultureChoices, query.CultureId));
        _maxItemWeight = query.MaxItemWeight;

        RebuildRows();

        HeaderText = GetSlotName(slot);

        OnPropertyChanged(nameof(IsWeaponSlot));
        OnPropertyChanged(nameof(WeaponClassText));
        OnPropertyChanged(nameof(CultureText));
        OnPropertyChanged(nameof(MaxItemWeight));
        OnPropertyChanged(nameof(MaxItemWeightText));
        // The search method may have changed in MCM since the popup last opened.
        OnPropertyChanged(nameof(IsPriorityMode));
        OnPropertyChanged(nameof(IsWeightsMode));
        OnPropertyChanged(nameof(IsEffectivenessMode));
        OnPropertyChanged(nameof(IsLockVisible));
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

    /// <summary>
    ///     Excludes the slot from searching: zeroes every weight (weights
    ///     mode) or empties the priority order (priority mode).
    /// </summary>
    public void ExecuteLock()
    {
        if (_character is null || _equipment is null) return;

        if (IsPriorityMode)
            _profiles.SetPriorities(_character, _equipment, _slot, Array.Empty<IReadOnlyList<ItemParam>>());
        else
            _profiles.SetWeights(_character, _equipment, _slot, new ParamWeights());
        RebuildRows();
        RefreshDefaultMarker();
        _onChanged();
    }

    /// <summary>Prints a deterministic account of this slot's pick to the message log, then closes.</summary>
    public void ExecuteExplain()
    {
        if (_character is null || _equipment is null) return;

        _explain(_character, _equipment, _slot);
        ExecuteClose();
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

        var count = WeaponCategoryChoices.Length;
        _weaponClassIndex = (_weaponClassIndex + step + count) % count;

        _profiles.SetWeaponCategory(_character, _equipment, _slot, WeaponCategoryChoices[_weaponClassIndex]);
        OnPropertyChanged(nameof(WeaponClassText));
        RefreshDefaultMarker();

        // Each weapon class exposes its own parameter set.
        RebuildRows();
        _onChanged();
    }

    private void RebuildRows()
    {
        if (_character is null || _equipment is null) return;

        if (IsEffectivenessMode)
        {
            Rows = new MBBindingList<ParamRowVM>();
            PriorityRows = new MBBindingList<PriorityRowVM>();
            return;
        }

        if (IsPriorityMode)
        {
            RebuildPriorityRows();
            return;
        }

        var query = _profiles.GetQuery(_character, _equipment, _slot);

        var rows = new MBBindingList<ParamRowVM>();
        foreach (var param in GetVisibleParams())
            rows.Add(new ParamRowVM(param, GetParamName(param), query.Weights[param], PersistWeights));
        Rows = rows;

        UpdateShares();
    }

    private void RebuildPriorityRows()
    {
        if (_character is null || _equipment is null) return;

        // GetQuery already normalized the stored order (and its groups) to
        // the class that matters right now — pinned, or "as equipped" the
        // equipped item's. A locked (empty) order displays as the defaults.
        var query = _profiles.GetQuery(_character, _equipment, _slot);
        var stored = query.Priorities is { Count: > 0 }
            ? query.Priorities
            : DefaultPriorities.GroupsFor(_slot,
                WeaponCategoryChoices[_weaponClassIndex]?.Class
                ?? _equipment[_slot].Item?.PrimaryWeapon?.WeaponClass);

        _priorityGroups.Clear();
        foreach (var group in stored)
            _priorityGroups.Add(new List<ItemParam>(group));

        RefreshPriorityRowsFromGroups();
    }

    private void RefreshPriorityRowsFromGroups()
    {
        var rows = new MBBindingList<PriorityRowVM>();
        for (var i = 0; i < _priorityGroups.Count; i++)
        {
            var chips = new List<PriorityChipVM>(_priorityGroups[i].Count);
            foreach (var param in _priorityGroups[i])
                chips.Add(new PriorityChipVM(param, GetParamName(param)));
            rows.Add(new PriorityRowVM(_priorityGroups[i], i, chips, LinkChip));
        }

        PriorityRows = rows;
    }

    /// <summary>
    ///     Gauntlet drop handler on the rows list: a chip dropped between rows
    ///     becomes its own rank at that position.
    /// </summary>
    public void ExecuteReorderChip(PriorityChipVM chip, int index, string tag)
    {
        var insertAt = Math.Max(0, Math.Min(index, _priorityGroups.Count));
        insertAt -= RemoveChip(chip.Param, insertAt);

        _priorityGroups.Insert(insertAt, new List<ItemParam> { chip.Param });
        PersistPriorities();
    }

    private void LinkChip(PriorityChipVM chip, PriorityRowVM row)
    {
        if (row.Group.Contains(chip.Param)) return;

        RemoveChip(chip.Param, _priorityGroups.Count);
        // The row keeps a reference to its backing group, so it stays valid
        // even when removing the chip collapsed an earlier group.
        if (!_priorityGroups.Contains(row.Group)) return;

        row.Group.Add(chip.Param);
        PersistPriorities();
    }

    /// <summary>
    ///     Pulls the stat out of whatever group holds it, dropping the group
    ///     when it empties.
    /// </summary>
    /// <returns>1 when a group before <paramref name="insertAt" /> collapsed (the caller's index shifts), else 0.</returns>
    private int RemoveChip(ItemParam param, int insertAt)
    {
        for (var i = 0; i < _priorityGroups.Count; i++)
        {
            if (!_priorityGroups[i].Remove(param)) continue;

            if (_priorityGroups[i].Count == 0)
            {
                _priorityGroups.RemoveAt(i);
                return i < insertAt ? 1 : 0;
            }

            return 0;
        }

        return 0;
    }

    private void PersistPriorities()
    {
        if (_character is null || _equipment is null) return;

        var order = new List<IReadOnlyList<ItemParam>>(_priorityGroups.Count);
        foreach (var group in _priorityGroups) order.Add(group.ToArray());
        _profiles.SetPriorities(_character, _equipment, _slot, order);

        RefreshPriorityRowsFromGroups();
        RefreshDefaultMarker();

        // Live preview, same as the weight sliders.
        _onChanged();
    }

    /// <summary>
    ///     Influence share of each visible weight: value / Σ|values|. The
    ///     scorer computes Σ(w·√v̂) / Σ|w|, so this is exactly the fraction of
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
            return WeaponCategoryChoices[_weaponClassIndex] is { } category
                ? GetParamsForWeaponClass(category.Class)
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
