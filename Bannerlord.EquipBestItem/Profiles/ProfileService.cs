using System.Collections.Generic;
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

        WeaponClass? pinnedClass =
            System.Enum.TryParse(profile.WeaponClass, true, out WeaponClass weaponClass) ? weaponClass : null;

        // Null weights = never customized (only a weapon class was pinned):
        // fall back to that class's defaults. An empty dictionary is different —
        // it is a locked slot (the player zeroed everything).
        var weights = profile.Weights is null
            ? DefaultWeights.For(slot, pinnedClass)
            : ParamWeights.FromDictionary(profile.Weights);

        var query = new ItemQuery(weights);
        if (pinnedClass is { } pinned) query.WeaponClass = pinned;
        if (!string.IsNullOrEmpty(profile.Culture)) query.CultureId = profile.Culture;
        if (profile.MaxItemWeight is { } maxWeight and > 0f) query.MaxItemWeight = maxWeight;
        return query;
    }

    public void SetWeights(CharacterObject character, Equipment equipment, EquipmentIndex slot, ParamWeights weights)
    {
        GetOrCreateSlotProfile(character, equipment, slot).Weights = weights.ToDictionary();
    }

    public void SetWeaponClass(CharacterObject character, Equipment equipment, EquipmentIndex slot, WeaponClass? weaponClass)
    {
        GetOrCreateSlotProfile(character, equipment, slot).WeaponClass = weaponClass?.ToString();
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
            WeaponClass = query.WeaponClass?.ToString(),
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
