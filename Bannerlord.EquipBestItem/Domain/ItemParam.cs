namespace Bannerlord.EquipBestItem.Domain;

/// <summary>
///     A scorable item parameter. Values are sequential and used as indices
///     into <see cref="ParamWeights" /> and stat vectors.
/// </summary>
public enum ItemParam
{
    HeadArmor,
    BodyArmor,
    ArmArmor,
    LegArmor,
    MountArmor,
    ChargeDamage,
    HitPoints,
    Maneuver,
    Speed,
    MaxAmmo,
    ThrustSpeed,
    SwingSpeed,
    MissileSpeed,
    MissileDamage,
    WeaponLength,
    ThrustDamage,
    SwingDamage,
    Accuracy,
    Handling,
    Weight,

    /// <summary>Armor's stealth bonus, for the stealth equipment set.</summary>
    Stealth
}

public static class ItemParams
{
    public const int Count = 21;
}
