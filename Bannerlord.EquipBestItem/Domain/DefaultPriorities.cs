using System.Collections.Generic;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain;

/// <summary>
///     Built-in stat-priority orders for the priority search mode: the same
///     parameters the weights popup shows for the slot (minus Weight — "the
///     heavier the better" is never what a priority means; the weight cap
///     constraint handles mass), importance-first. The player reorders them
///     per slot; these are the starting point.
/// </summary>
public static class DefaultPriorities
{
    private static readonly ItemParam[] Head = Armor(ItemParam.HeadArmor);
    private static readonly ItemParam[] Body = Armor(ItemParam.BodyArmor, ItemParam.ArmArmor, ItemParam.LegArmor);
    private static readonly ItemParam[] Leg = Armor(ItemParam.LegArmor);
    private static readonly ItemParam[] Gloves = Armor(ItemParam.ArmArmor);
    private static readonly ItemParam[] Cape = Armor(ItemParam.BodyArmor, ItemParam.ArmArmor);
    private static readonly ItemParam[] Harness = { ItemParam.MountArmor };

    /// <summary>
    ///     A worn-armor order with stealth ranked last where the game has that
    ///     stat: protection is what an armor slot is for, but the stat still
    ///     needs a chip the player can drag up in the stealth set.
    /// </summary>
    private static ItemParam[] Armor(params ItemParam[] protection)
    {
        if (!Compat.GameCompat.SupportsStealth) return protection;

        var order = new ItemParam[protection.Length + 1];
        protection.CopyTo(order, 0);
        order[protection.Length] = ItemParam.Stealth;
        return order;
    }

    private static readonly ItemParam[] Horse =
        { ItemParam.Speed, ItemParam.Maneuver, ItemParam.ChargeDamage, ItemParam.HitPoints };

    // "As equipped" weapon slots can hold anything, so every weapon stat is present.
    private static readonly ItemParam[] AnyWeapon =
    {
        ItemParam.ThrustDamage, ItemParam.SwingDamage, ItemParam.ThrustSpeed, ItemParam.SwingSpeed,
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy, ItemParam.Handling,
        ItemParam.WeaponLength, ItemParam.MaxAmmo, ItemParam.HitPoints
    };

    private static readonly ItemParam[] Melee =
    {
        ItemParam.ThrustDamage, ItemParam.SwingDamage, ItemParam.ThrustSpeed, ItemParam.SwingSpeed,
        ItemParam.WeaponLength, ItemParam.Handling
    };

    private static readonly ItemParam[] Bow =
    {
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy, ItemParam.SwingSpeed,
        ItemParam.ThrustSpeed, ItemParam.WeaponLength
    };

    private static readonly ItemParam[] Crossbow =
    {
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy, ItemParam.SwingSpeed,
        ItemParam.MaxAmmo, ItemParam.ThrustSpeed, ItemParam.WeaponLength
    };

    private static readonly ItemParam[] Ammo = { ItemParam.MissileDamage, ItemParam.MaxAmmo };

    // Throwing stats first (a thrown weapon's main role); the melee-usage stats
    // follow as low-priority tie-breakers the player can drag up — available in
    // priority mode the way they are in the weights popup, without changing the
    // default pick (they only decide when every throwing stat above ties).
    private static readonly ItemParam[] Thrown =
    {
        ItemParam.MissileDamage, ItemParam.MissileSpeed, ItemParam.Accuracy,
        ItemParam.WeaponLength, ItemParam.MaxAmmo,
        ItemParam.ThrustDamage, ItemParam.SwingDamage, ItemParam.ThrustSpeed, ItemParam.SwingSpeed,
        ItemParam.Handling
    };

    private static readonly ItemParam[] Shield =
    {
        ItemParam.HitPoints, ItemParam.SwingSpeed, ItemParam.BodyArmor,
        ItemParam.ThrustSpeed, ItemParam.WeaponLength
    };

    // Group views over the flat sets (each stat its own group), cached so the
    // comparer's per-pair fallback never allocates.
    private static readonly Dictionary<IReadOnlyList<ItemParam>, IReadOnlyList<IReadOnlyList<ItemParam>>>
        GroupCache = new();

    /// <summary>The default order as singleton groups. The result is shared — treat it as read-only.</summary>
    public static IReadOnlyList<IReadOnlyList<ItemParam>> GroupsFor(EquipmentIndex slot, WeaponClass? weaponClass)
    {
        var flat = For(slot, weaponClass);

        lock (GroupCache)
        {
            if (GroupCache.TryGetValue(flat, out var cached)) return cached;

            var groups = new IReadOnlyList<ItemParam>[flat.Count];
            for (var i = 0; i < flat.Count; i++)
                groups[i] = new[] { flat[i] };
            GroupCache[flat] = groups;
            return groups;
        }
    }

    /// <summary>The returned array is shared — treat it as read-only.</summary>
    public static IReadOnlyList<ItemParam> For(EquipmentIndex slot, WeaponClass? weaponClass)
    {
        if (slot >= EquipmentIndex.Weapon0 && slot <= EquipmentIndex.Weapon3)
            return weaponClass is { } pinned ? ForWeaponClass(pinned) : AnyWeapon;

        return slot switch
        {
            EquipmentIndex.Head => Head,
            EquipmentIndex.Body => Body,
            EquipmentIndex.Leg => Leg,
            EquipmentIndex.Gloves => Gloves,
            EquipmentIndex.Cape => Cape,
            EquipmentIndex.Horse => Horse,
            EquipmentIndex.HorseHarness => Harness,
            _ => Head
        };
    }

    private static IReadOnlyList<ItemParam> ForWeaponClass(WeaponClass weaponClass) => weaponClass switch
    {
        WeaponClass.Bow => Bow,
        WeaponClass.Crossbow => Crossbow,
        WeaponClass.Arrow or WeaponClass.Bolt => Ammo,
        WeaponClass.Javelin or WeaponClass.ThrowingAxe or WeaponClass.ThrowingKnife => Thrown,
        WeaponClass.SmallShield or WeaponClass.LargeShield => Shield,
        _ => Melee
    };
}
