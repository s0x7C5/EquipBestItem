using System;
using System.Collections.Generic;
using Bannerlord.EquipBestItem.Domain;
using Newtonsoft.Json;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Ai;

/// <summary>
///     Parses the model's JSON reply into an <see cref="InterpretedPlan" />,
///     expanding group slots (AllArmor, AllWeapons, All) into concrete ones.
/// </summary>
public static class LlmPlanParser
{
    private static readonly EquipmentIndex[] ArmorSlots =
    {
        EquipmentIndex.Head, EquipmentIndex.Cape, EquipmentIndex.Body,
        EquipmentIndex.Gloves, EquipmentIndex.Leg
    };

    private static readonly EquipmentIndex[] WeaponSlots =
    {
        EquipmentIndex.Weapon0, EquipmentIndex.Weapon1,
        EquipmentIndex.Weapon2, EquipmentIndex.Weapon3
    };

    private static readonly EquipmentIndex[] MountSlots =
    {
        EquipmentIndex.Horse, EquipmentIndex.HorseHarness
    };

    public static InterpretedPlan Parse(string json)
    {
        var dto = JsonConvert.DeserializeObject<PlanDto>(ExtractJson(json))
                  ?? throw new FormatException("Empty AI response.");

        var directives = new List<EquipDirective>();

        foreach (var directiveDto in dto.Directives ?? new List<DirectiveDto>())
        {
            if (directiveDto.Slot is null) continue;

            foreach (var slot in ExpandSlots(directiveDto.Slot))
                directives.Add(new EquipDirective(slot, BuildQuery(directiveDto, slot)));
        }

        if (directives.Count == 0)
            throw new FormatException("The AI response contains no usable directives.");

        return new InterpretedPlan(directives, dto.Explanation ?? "", dto.Target ?? "");
    }

    private static ItemQuery BuildQuery(DirectiveDto dto, EquipmentIndex slot)
    {
        var weights = ParamWeights.FromDictionary(dto.Weights);

        WeaponClass? pinnedClass =
            Enum.TryParse(dto.WeaponClass, true, out WeaponClass weaponClass) &&
            weaponClass != WeaponClass.Undefined
                ? weaponClass
                : null;

        // A directive without weights means "just find the best": use the
        // slot (or pinned class) defaults, but let the player's search
        // method setting decide.
        var query = weights.IsEmpty
            ? new ItemQuery(Profiles.DefaultWeights.For(slot, pinnedClass))
            : new ItemQuery(weights) { HasExplicitWeights = true };

        if (dto.MaxItemWeight is { } maxWeight and > 0f)
            query.MaxItemWeight = maxWeight;

        if (!string.IsNullOrEmpty(dto.Culture))
            query.CultureId = dto.Culture;

        query.WeaponClass = pinnedClass;
        return query;
    }

    private static IEnumerable<EquipmentIndex> ExpandSlots(string slot)
    {
        switch (slot.Trim().ToLowerInvariant())
        {
            case "allarmor":
                return ArmorSlots;
            case "allweapons":
                return WeaponSlots;
            case "allmount":
                return MountSlots;
            case "all":
                var all = new List<EquipmentIndex>(ArmorSlots.Length + WeaponSlots.Length + MountSlots.Length);
                all.AddRange(ArmorSlots);
                all.AddRange(WeaponSlots);
                all.AddRange(MountSlots);
                return all;
            default:
                return Enum.TryParse(slot, true, out EquipmentIndex parsed)
                    ? new[] { parsed }
                    : Array.Empty<EquipmentIndex>();
        }
    }

    /// <summary>Tolerates replies wrapped in markdown code fences or prose.</summary>
    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : text;
    }

    private sealed class PlanDto
    {
        [JsonProperty("explanation")] public string? Explanation { get; set; }

        [JsonProperty("target")] public string? Target { get; set; }

        [JsonProperty("directives")] public List<DirectiveDto>? Directives { get; set; }
    }

    private sealed class DirectiveDto
    {
        [JsonProperty("slot")] public string? Slot { get; set; }

        [JsonProperty("weights")] public Dictionary<string, float>? Weights { get; set; }

        [JsonProperty("maxItemWeight")] public float? MaxItemWeight { get; set; }

        [JsonProperty("culture")] public string? Culture { get; set; }

        [JsonProperty("weaponClass")] public string? WeaponClass { get; set; }
    }
}
