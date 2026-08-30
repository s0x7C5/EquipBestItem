using Bannerlord.EquipBestItem.Compat;
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
                stats[(int)ItemParam.Stealth] = GameCompat.GetStealthFactor(element);
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

        // A weapon can carry two usages: a melee mode and a ranged one (thrown
        // javelins/axes/knives have both). Each stat must be read from the usage
        // that legitimately owns it — missile stats are only valid on the ranged
        // usage (they read 0 on a melee usage), melee stats on the melee usage.
        // Reading everything from usage 0 loses the thrown mode entirely. Pure
        // melee weapons have no ranged usage and pure ranged weapons (bows) no
        // melee usage, so each falls back to usage 0 — unchanged behavior.
        var weapons = item.Weapons;
        var meleeUsage = -1;
        var rangedUsage = -1;
        for (var i = 0; weapons is not null && i < weapons.Count; i++)
        {
            var usage = weapons[i];
            if (meleeUsage < 0 && usage.IsMeleeWeapon) meleeUsage = i;
            if (rangedUsage < 0 && (usage.IsRangedWeapon || usage.IsConsumable)) rangedUsage = i;
        }

        if (meleeUsage < 0) meleeUsage = 0;

        stats[(int)ItemParam.ThrustSpeed] = element.GetModifiedThrustSpeedForUsage(meleeUsage);
        stats[(int)ItemParam.SwingSpeed] = element.GetModifiedSwingSpeedForUsage(meleeUsage);
        stats[(int)ItemParam.ThrustDamage] = element.GetModifiedThrustDamageForUsage(meleeUsage);
        stats[(int)ItemParam.SwingDamage] = element.GetModifiedSwingDamageForUsage(meleeUsage);
        stats[(int)ItemParam.Handling] = element.GetModifiedHandlingForUsage(meleeUsage);
        stats[(int)ItemParam.WeaponLength] = weapon.WeaponLength;

        // Missile stats stay 0 when the weapon has no ranged mode.
        if (rangedUsage >= 0)
        {
            stats[(int)ItemParam.MaxAmmo] = element.GetModifiedStackCountForUsage(rangedUsage);
            stats[(int)ItemParam.MissileSpeed] = element.GetModifiedMissileSpeedForUsage(rangedUsage);
            stats[(int)ItemParam.MissileDamage] = element.GetModifiedMissileDamageForUsage(rangedUsage);
            stats[(int)ItemParam.Accuracy] = weapons![rangedUsage].Accuracy;
        }

        if (weapon.IsShield)
        {
            stats[(int)ItemParam.HitPoints] = element.GetModifiedMaximumHitPointsForUsage(0);
            stats[(int)ItemParam.BodyArmor] = weapon.BodyArmor;
        }
    }
}
