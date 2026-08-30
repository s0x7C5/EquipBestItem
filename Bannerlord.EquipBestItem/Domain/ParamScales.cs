namespace Bannerlord.EquipBestItem.Domain;

/// <summary>
///     Per-parameter reference scales: a "strong item" value for each stat,
///     used to bring parameters onto a common 0..~1 range before the weighted
///     sum. Raw stats live on wildly different scales (a shield's hit points
///     are hundreds, its body armor is single digits, a mount's maneuver is
///     tens), so a plain weighted sum lets the biggest-number parameter drown
///     out the rest. Dividing by a fixed reference keeps a weight of 1 on hit
///     points comparable to a weight of 1 on maneuver, while — unlike
///     normalizing over the current search pool — preserving magnitude: a
///     540-vs-300 hit-point gap stays large instead of collapsing to "one is
///     bigger". Fixed (not pool-relative) means an item's score does not shift
///     with whatever else happens to be in the inventory, so the pick is
///     stable and does not flip between equips.
///
///     Values are sanitized round numbers in the ballpark of a top-tier
///     vanilla item's stat; a value above its reference simply scores above 1,
///     which is fine — the reference is a scale, not a cap. Since a slot search
///     only ever compares items of one type, the shared HitPoints reference
///     (shields reach ~750, mounts ~350) is a deliberate middle ground that
///     keeps hit points meaningful for both without letting them dominate.
/// </summary>
public static class ParamScales
{
    private static readonly float[] Scale = Build();

    /// <summary>1 / reference, so the hot scoring path multiplies instead of divides.</summary>
    public static readonly float[] Inverse = BuildInverse();

    private static float[] Build()
    {
        var scale = new float[ItemParams.Count];

        scale[(int)ItemParam.HeadArmor] = 60f;
        scale[(int)ItemParam.BodyArmor] = 80f;
        scale[(int)ItemParam.ArmArmor] = 55f;
        scale[(int)ItemParam.LegArmor] = 55f;
        scale[(int)ItemParam.MountArmor] = 55f;
        scale[(int)ItemParam.ChargeDamage] = 40f;
        scale[(int)ItemParam.HitPoints] = 600f;
        scale[(int)ItemParam.Maneuver] = 90f;
        scale[(int)ItemParam.Speed] = 75f;
        scale[(int)ItemParam.MaxAmmo] = 40f;
        scale[(int)ItemParam.ThrustSpeed] = 130f;
        scale[(int)ItemParam.SwingSpeed] = 130f;
        scale[(int)ItemParam.MissileSpeed] = 100f;
        scale[(int)ItemParam.MissileDamage] = 120f;
        scale[(int)ItemParam.WeaponLength] = 250f;
        scale[(int)ItemParam.ThrustDamage] = 100f;
        scale[(int)ItemParam.SwingDamage] = 100f;
        scale[(int)ItemParam.Accuracy] = 100f;
        scale[(int)ItemParam.Handling] = 100f;
        scale[(int)ItemParam.Weight] = 30f;
        scale[(int)ItemParam.Stealth] = 30f;

        return scale;
    }

    private static float[] BuildInverse()
    {
        var scale = Build();
        var inverse = new float[scale.Length];
        for (var i = 0; i < scale.Length; i++)
            inverse[i] = scale[i] > 0f ? 1f / scale[i] : 0f;
        return inverse;
    }
}
