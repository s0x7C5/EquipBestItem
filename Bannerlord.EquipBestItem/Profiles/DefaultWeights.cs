using Bannerlord.EquipBestItem.Domain;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Profiles;

/// <summary>
///     Built-in starting weights per slot, mirroring the v2 defaults: every
///     parameter relevant to the slot participates equally until the player
///     tunes it. A slot whose weights are all zeroed by the player is
///     excluded from searching.
/// </summary>
public static class DefaultWeights
{
    public static ParamWeights For(EquipmentIndex slot)
    {
        var weights = new ParamWeights();

        if (slot >= EquipmentIndex.Weapon0 && slot <= EquipmentIndex.Weapon3)
        {
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
}
