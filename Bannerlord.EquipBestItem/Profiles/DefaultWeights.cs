using Bannerlord.EquipBestItem.Domain;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Profiles;

/// <summary>
///     Built-in starting weights, mirroring the v2 defaults: every parameter
///     relevant to the slot — or to the pinned weapon class — participates
///     equally until the player tunes it. A slot whose weights are all zeroed
///     by the player is excluded from searching.
/// </summary>
public static class DefaultWeights
{
    public static ParamWeights For(EquipmentIndex slot) => For(slot, null);

    public static ParamWeights For(EquipmentIndex slot, WeaponClass? weaponClass)
    {
        var weights = new ParamWeights();

        if (slot >= EquipmentIndex.Weapon0 && slot <= EquipmentIndex.Weapon3)
        {
            if (weaponClass is { } pinned)
            {
                FillForWeaponClass(weights, pinned);
                return weights;
            }

            weights[ItemParam.ThrustDamage] = 1f;
            weights[ItemParam.SwingDamage] = 1f;
            weights[ItemParam.ThrustSpeed] = 1f;
            weights[ItemParam.SwingSpeed] = 1f;
            weights[ItemParam.MissileDamage] = 1f;
            weights[ItemParam.MissileSpeed] = 1f;
            weights[ItemParam.Accuracy] = 1f;
            weights[ItemParam.Handling] = 1f;
            weights[ItemParam.WeaponLength] = 1f;
            weights[ItemParam.MaxAmmo] = 1f;
            weights[ItemParam.HitPoints] = 1f;
            return weights;
        }

        switch (slot)
        {
            case EquipmentIndex.Head:
                weights[ItemParam.HeadArmor] = 1f;
                break;
            case EquipmentIndex.Body:
                weights[ItemParam.BodyArmor] = 1f;
                weights[ItemParam.ArmArmor] = 1f;
                weights[ItemParam.LegArmor] = 1f;
                break;
            case EquipmentIndex.Leg:
                weights[ItemParam.LegArmor] = 1f;
                break;
            case EquipmentIndex.Gloves:
                weights[ItemParam.ArmArmor] = 1f;
                break;
            case EquipmentIndex.Cape:
                weights[ItemParam.BodyArmor] = 1f;
                weights[ItemParam.ArmArmor] = 1f;
                break;
            case EquipmentIndex.Horse:
                weights[ItemParam.Speed] = 1f;
                weights[ItemParam.Maneuver] = 1f;
                weights[ItemParam.ChargeDamage] = 1f;
                weights[ItemParam.HitPoints] = 1f;
                break;
            case EquipmentIndex.HorseHarness:
                weights[ItemParam.MountArmor] = 1f;
                break;
        }

        return weights;
    }

    /// <summary>
    ///     The parameters that matter for a weapon class, matching the sets the
    ///     weights popup shows for it (minus Weight, which stays neutral).
    /// </summary>
    private static void FillForWeaponClass(ParamWeights weights, WeaponClass weaponClass)
    {
        switch (weaponClass)
        {
            // The "Speed" the game prints on bows, crossbows and shields is the
            // SWING speed (speed_rating); their thrust_speed is a separate,
            // never-displayed number.
            case WeaponClass.Bow:
                weights[ItemParam.MissileDamage] = 1f;
                weights[ItemParam.MissileSpeed] = 1f;
                weights[ItemParam.Accuracy] = 1f;
                weights[ItemParam.SwingSpeed] = 1f;
                break;
            case WeaponClass.Crossbow:
                weights[ItemParam.MissileDamage] = 1f;
                weights[ItemParam.MissileSpeed] = 1f;
                weights[ItemParam.Accuracy] = 1f;
                weights[ItemParam.SwingSpeed] = 1f;
                weights[ItemParam.MaxAmmo] = 1f;
                break;
            case WeaponClass.Arrow:
            case WeaponClass.Bolt:
                weights[ItemParam.MissileDamage] = 1f;
                weights[ItemParam.MaxAmmo] = 1f;
                break;
            case WeaponClass.Javelin:
            case WeaponClass.ThrowingAxe:
            case WeaponClass.ThrowingKnife:
                weights[ItemParam.MissileDamage] = 1f;
                weights[ItemParam.MissileSpeed] = 1f;
                weights[ItemParam.Accuracy] = 1f;
                weights[ItemParam.WeaponLength] = 1f;
                weights[ItemParam.MaxAmmo] = 1f;
                break;
            // The game's shield tooltip shows only Hit Points and Swing Speed.
            // Body armor and length are hidden and quirky for shields — an oval
            // shield's length is 435 in the data, a mesh artifact, not reach —
            // so they stay off by default (available as optional sliders).
            case WeaponClass.SmallShield:
            case WeaponClass.LargeShield:
                weights[ItemParam.HitPoints] = 1f;
                weights[ItemParam.SwingSpeed] = 1f;
                break;
            default: // melee: swords, axes, maces, polearms, daggers
                weights[ItemParam.ThrustDamage] = 1f;
                weights[ItemParam.SwingDamage] = 1f;
                weights[ItemParam.ThrustSpeed] = 1f;
                weights[ItemParam.SwingSpeed] = 1f;
                weights[ItemParam.WeaponLength] = 1f;
                weights[ItemParam.Handling] = 1f;
                break;
        }
    }
}
