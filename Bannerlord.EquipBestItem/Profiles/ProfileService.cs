using System.Collections.Generic;
using System.Linq;
using Bannerlord.EquipBestItem.Domain;
using Bannerlord.EquipBestItem.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Profiles;

/// <summary>
///     Per-character, per-equipment-set, per-slot search preferences with
///     built-in defaults and JSON persistence.
/// </summary>
public sealed class ProfileService
{
    private const string FileName = "profiles.json";

    private readonly JsonFileStore _store;
    private readonly ProfileData _data;

    public ProfileService(JsonFileStore store)
    {
        _store = store;
        _data = _store.Load(FileName, () => new ProfileData());
    }

    public ItemQuery GetQuery(CharacterObject character, Equipment equipment, EquipmentIndex slot)
    {
        var profile = FindSlotProfile(character, equipment, slot);

        // No personal override: the player-edited slot default applies, and
        // only then the built-in weights.
        if (profile is null && _data.Defaults.TryGetValue(slot.ToString(), out var playerDefault))
            profile = playerDefault;

        if (profile is null) return new ItemQuery(DefaultWeights.For(slot));

        var pinnedCategory = WeaponCategory.Parse(profile.WeaponClass);

        // Null weights = never customized (only a weapon class was pinned):
        // fall back to that class's defaults. An empty dictionary is different —
        // it is a locked slot (the player zeroed everything).
        var weights = profile.Weights is null
            ? DefaultWeights.For(slot, pinnedCategory?.Class)
            : ParamWeights.FromDictionary(profile.Weights);

        var query = new ItemQuery(weights);
        if (pinnedCategory is { } pinned) query.WeaponCategory = pinned;
        if (!string.IsNullOrEmpty(profile.Culture)) query.CultureId = profile.Culture;
        if (profile.MaxItemWeight is { } maxWeight and > 0f) query.MaxItemWeight = maxWeight;
        query.Priorities = NormalizePriorities(ParsePriorities(profile.Priorities), slot, query, equipment);
        return query;
    }

    /// <summary>
    ///     Null stays null (defaults apply); unknown stat names are dropped.
    ///     One entry is a group of equal-rank stats joined with '+'
    ///     ("HitPoints+BodyArmor"); plain names parse as single-stat groups,
    ///     which keeps orders saved before groups existed working.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<ItemParam>>? ParsePriorities(List<string>? names)
    {
        if (names is null) return null;

        var seen = new HashSet<ItemParam>();
        var result = new List<IReadOnlyList<ItemParam>>(names.Count);
        foreach (var name in names)
        {
            var group = new List<ItemParam>();
            foreach (var part in name.Split('+'))
                if (System.Enum.TryParse(part.Trim(), true, out ItemParam param) && seen.Add(param))
                    group.Add(param);
            if (group.Count > 0) result.Add(group);
        }

        return result;
    }

    /// <summary>
    ///     Fits a stored order to the stats that matter for the slot right
    ///     now (the pinned class, or for "as equipped" the equipped item's
    ///     class): keeps the stored ranking and grouping of known stats,
    ///     appends the missing ones as single groups in default order.
    ///     Without this, an order saved while a bow was equipped would
    ///     compare maces on absent bow stats and tie everything. Null
    ///     (defaults) and empty (locked) pass through.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<ItemParam>>? NormalizePriorities(
        IReadOnlyList<IReadOnlyList<ItemParam>>? stored, EquipmentIndex slot, ItemQuery query, Equipment equipment)
    {
        if (stored is null || stored.Count == 0) return stored;

        var defaults = DefaultPriorities.For(slot,
            query.WeaponCategory?.Class ?? equipment[slot].Item?.PrimaryWeapon?.WeaponClass);

        var seen = new HashSet<ItemParam>();
        var result = new List<IReadOnlyList<ItemParam>>(defaults.Count);
        foreach (var storedGroup in stored)
        {
            var group = new List<ItemParam>(storedGroup.Count);
            foreach (var param in storedGroup)
                if (defaults.Contains(param) && seen.Add(param))
                    group.Add(param);
            if (group.Count > 0) result.Add(group);
        }

        foreach (var param in defaults)
            if (seen.Add(param))
                result.Add(new[] { param });
        return result;
    }

    public void SetWeights(CharacterObject character, Equipment equipment, EquipmentIndex slot, ParamWeights weights)
    {
        GetOrCreateSlotProfile(character, equipment, slot).Weights = weights.ToDictionary();
    }

    public void SetWeaponCategory(CharacterObject character, Equipment equipment, EquipmentIndex slot, WeaponCategory? category)
    {
        GetOrCreateSlotProfile(character, equipment, slot).WeaponClass = category?.ToString();
    }

    /// <summary>Null resets to the default order; an empty list locks the slot for priority searches.</summary>
    public void SetPriorities(
        CharacterObject character, Equipment equipment, EquipmentIndex slot,
        IReadOnlyList<IReadOnlyList<ItemParam>>? order)
    {
        GetOrCreateSlotProfile(character, equipment, slot).Priorities = ToNames(order);
    }

    private static List<string>? ToNames(IReadOnlyList<IReadOnlyList<ItemParam>>? order)
    {
        if (order is null) return null;

        var names = new List<string>(order.Count);
        foreach (var group in order)
            names.Add(string.Join("+", group));
        return names;
    }

    /// <summary>Hard constraints; null clears the constraint.</summary>
    public void SetConstraints(
        CharacterObject character, Equipment equipment, EquipmentIndex slot,
        string? cultureId, float? maxItemWeight)
    {
        var profile = GetOrCreateSlotProfile(character, equipment, slot);
        profile.Culture = string.IsNullOrEmpty(cultureId) ? null : cultureId;
        profile.MaxItemWeight = maxItemWeight is > 0f ? maxItemWeight : null;
    }

    /// <summary>True when the hero has their own settings for the slot (is not following the defaults).</summary>
    public bool HasOverride(CharacterObject character, Equipment equipment, EquipmentIndex slot) =>
        FindSlotProfile(character, equipment, slot) is not null;

    /// <summary>
    ///     The hero's current effective filter becomes the slot's default for
    ///     every hero without an override; this hero's own override is dropped
    ///     so they follow the default from now on too.
    /// </summary>
    public void SaveAsDefault(CharacterObject character, Equipment equipment, EquipmentIndex slot)
    {
        var query = GetQuery(character, equipment, slot);

        _data.Defaults[slot.ToString()] = new SlotProfileData
        {
            Weights = query.Weights.ToDictionary(),
            Priorities = ToNames(query.Priorities),
            WeaponClass = query.WeaponCategory?.ToString(),
            Culture = query.CultureId,
            MaxItemWeight = query.MaxItemWeight > 0f ? query.MaxItemWeight : null
        };

        ResetSlot(character, equipment, slot);
    }

    public void ResetSlot(CharacterObject character, Equipment equipment, EquipmentIndex slot)
    {
        if (_data.Characters.TryGetValue(character.StringId, out var sets) &&
            sets.TryGetValue(GetSetKey(equipment), out var slots))
            slots.Remove(slot.ToString());
    }

    public void Save()
    {
        _store.Save(FileName, _data);
    }

    private SlotProfileData? FindSlotProfile(CharacterObject character, Equipment equipment, EquipmentIndex slot)
    {
        return _data.Characters.TryGetValue(character.StringId, out var sets) &&
               sets.TryGetValue(GetSetKey(equipment), out var slots) &&
               slots.TryGetValue(slot.ToString(), out var profile)
            ? profile
            : null;
    }

    private SlotProfileData GetOrCreateSlotProfile(CharacterObject character, Equipment equipment, EquipmentIndex slot)
    {
        if (!_data.Characters.TryGetValue(character.StringId, out var sets))
            _data.Characters[character.StringId] = sets = new Dictionary<string, Dictionary<string, SlotProfileData>>();

        var setKey = GetSetKey(equipment);
        if (!sets.TryGetValue(setKey, out var slots))
            sets[setKey] = slots = new Dictionary<string, SlotProfileData>();

        var slotKey = slot.ToString();
        if (!slots.TryGetValue(slotKey, out var profile))
            slots[slotKey] = profile = new SlotProfileData();

        return profile;
    }

    private static string GetSetKey(Equipment equipment) =>
        equipment.IsCivilian ? "civilian" : equipment.IsStealth ? "stealth" : "battle";
}
