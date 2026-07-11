using System.Collections.Generic;

namespace Bannerlord.EquipBestItem.Profiles;

/// <summary>Serialized shape of per-character search preferences.</summary>
public sealed class ProfileData
{
    /// <summary>Hero string id → equipment set key ("battle"/"civilian"/"stealth") → slot name → slot profile.</summary>
    public Dictionary<string, Dictionary<string, Dictionary<string, SlotProfileData>>> Characters { get; set; } = new();
}

public sealed class SlotProfileData
{
    public Dictionary<string, float>? Weights { get; set; }

    public string? WeaponClass { get; set; }

    /// <summary>Restrict the search to items of this culture (e.g. "empire").</summary>
    public string? Culture { get; set; }

    /// <summary>Skip items heavier than this, kg.</summary>
    public float? MaxItemWeight { get; set; }
}
