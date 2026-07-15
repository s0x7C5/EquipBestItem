using System;
using System.Collections.Concurrent;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.EquipBestItem.Domain;

/// <summary>
///     A pinnable weapon category. One-to-one with the game's weapon classes,
///     except bows: short and long bows share <see cref="WeaponClass.Bow" />
///     but a long bow cannot be fired from horseback, so the mod treats them
///     as two separate categories and never swaps one for the other.
/// </summary>
public readonly struct WeaponCategory : IEquatable<WeaponCategory>
{
    // Usage-set flags come from a native engine call; memoize per usage string.
    private static readonly ConcurrentDictionary<string, bool> LongBowUsageCache = new();

    private WeaponCategory(WeaponClass weaponClass, bool isLongBow)
    {
        Class = weaponClass;
        IsLongBow = isLongBow;
    }

    public WeaponClass Class { get; }

    /// <summary>Meaningful only when <see cref="Class" /> is Bow.</summary>
    public bool IsLongBow { get; }

    public static readonly WeaponCategory ShortBow = new(WeaponClass.Bow, false);
    public static readonly WeaponCategory LongBow = new(WeaponClass.Bow, true);

    public static WeaponCategory Of(WeaponClass weaponClass) =>
        weaponClass == WeaponClass.Bow ? ShortBow : new WeaponCategory(weaponClass, false);

    public bool Matches(WeaponComponentData weapon)
    {
        if (weapon.WeaponClass != Class) return false;
        return Class != WeaponClass.Bow || IsLongBowUsage(weapon.ItemUsage) == IsLongBow;
    }

    /// <summary>
    ///     A bow is "long" when its usage set forbids shooting from horseback —
    ///     true for the vanilla "long_bow" usage and for modded bows that carry
    ///     the same flag.
    /// </summary>
    public static bool IsLongBowUsage(string? itemUsage)
    {
        if (string.IsNullOrEmpty(itemUsage)) return false;

        return LongBowUsageCache.GetOrAdd(itemUsage!, static usage =>
            (MBItem.GetItemUsageSetFlags(usage) & ItemObject.ItemUsageSetFlags.RequiresNoMount) != 0);
    }

    /// <summary>The persisted/AI token: the weapon class name, or LongBow/ShortBow.</summary>
    public override string ToString() =>
        Class == WeaponClass.Bow ? (IsLongBow ? "LongBow" : "ShortBow") : Class.ToString();

    /// <summary>
    ///     Parses a persisted or AI-provided token. A plain "Bow" (legacy saves,
    ///     careless models) means the short bow — the kind usable everywhere.
    /// </summary>
    public static WeaponCategory? Parse(string? token)
    {
        if (token is null) return null;

        var trimmed = token.Trim();
        if (trimmed.Length == 0) return null;

        var compact = trimmed.Replace(" ", "").Replace("_", "");
        if (compact.Equals("LongBow", StringComparison.OrdinalIgnoreCase)) return LongBow;
        if (compact.Equals("ShortBow", StringComparison.OrdinalIgnoreCase)) return ShortBow;

        return Enum.TryParse(compact, true, out WeaponClass weaponClass) &&
               weaponClass != WeaponClass.Undefined
            ? Of(weaponClass)
            : null;
    }

    public bool Equals(WeaponCategory other) => Class == other.Class && IsLongBow == other.IsLongBow;

    public override bool Equals(object? obj) => obj is WeaponCategory other && Equals(other);

    public override int GetHashCode() => ((int)Class << 1) | (IsLongBow ? 1 : 0);
}
