using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain;

/// <summary>
///     The single place that knows how to read raw item numbers (with item
///     modifiers applied) into a flat stat vector indexed by <see cref="ItemParam" />.
/// </summary>
public static class ItemStatExtractor
{
    /// <param name="element">The item to read.</param>
    /// <param name="harness">Current horse harness; mount stats depend on it.</param>
    /// <param name="stats">Destination vector of length <see cref="ItemParams.Count" />.</param>
    public static void Fill(EquipmentElement element, EquipmentElement harness, float[] stats)
    {
        for (var i = 0; i < stats.Length; i++)
            stats[i] = 0f;

        var item = element.Item;
        if (item is null) return;

        stats[(int)ItemParam.Weight] = item.Weight;

        if (item.HasArmorComponent)
        {
            if (item.ItemType == ItemObject.ItemTypeEnum.HorseHarness)
            {
                stats[(int)ItemParam.MountArmor] = element.GetModifiedMountBodyArmor();
            }
            else
            {
                stats[(int)ItemParam.HeadArmor] = element.GetModifiedHeadArmor();
                stats[(int)ItemParam.BodyArmor] = element.GetModifiedBodyArmor();
                stats[(int)ItemParam.ArmArmor] = element.GetModifiedArmArmor();
                stats[(int)ItemParam.LegArmor] = element.GetModifiedLegArmor();
            }
            return;
        }

        if (item.HasHorseComponent)
        {
            stats[(int)ItemParam.ChargeDamage] = element.GetModifiedMountCharge(in harness);
            stats[(int)ItemParam.Maneuver] = element.GetModifiedMountManeuver(in harness);
            stats[(int)ItemParam.Speed] = element.GetModifiedMountSpeed(in harness);
            stats[(int)ItemParam.HitPoints] = element.GetModifiedMountHitPoints();
            return;
        }

        var weapon = item.PrimaryWeapon;
        if (weapon is null) return;

        stats[(int)ItemParam.MaxAmmo] = element.GetModifiedStackCountForUsage(0);
        stats[(int)ItemParam.ThrustSpeed] = element.GetModifiedThrustSpeedForUsage(0);
        stats[(int)ItemParam.SwingSpeed] = element.GetModifiedSwingSpeedForUsage(0);
        stats[(int)ItemParam.MissileSpeed] = element.GetModifiedMissileSpeedForUsage(0);
        stats[(int)ItemParam.MissileDamage] = element.GetModifiedMissileDamageForUsage(0);
        stats[(int)ItemParam.ThrustDamage] = element.GetModifiedThrustDamageForUsage(0);
        stats[(int)ItemParam.SwingDamage] = element.GetModifiedSwingDamageForUsage(0);
        stats[(int)ItemParam.Handling] = element.GetModifiedHandlingForUsage(0);
        stats[(int)ItemParam.WeaponLength] = weapon.WeaponLength;
        stats[(int)ItemParam.Accuracy] = weapon.Accuracy;

        if (weapon.IsShield)
            stats[(int)ItemParam.HitPoints] = element.GetModifiedMaximumHitPointsForUsage(0);
    }
}
