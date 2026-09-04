using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Bannerlord.EquipBestItem.Domain;

/// <summary>
///     The culture ids that actually tag equipment in the catalog — DLC (War
///     Sails' Nords) and mod cultures included — so the culture filter and the
///     AI are not limited to the base game's six kingdoms. Bandit/pirate
///     cultures are dropped: you don't filter gear by "Corsairs". Built from the
///     item catalog on the main thread and cached for the session (cultures
///     don't change mid-campaign). Must be called on the main thread — it reads
///     game objects.
/// </summary>
public static class ItemCultures
{
    private static IReadOnlyList<string>? _ids;

    public static IReadOnlyList<string> WithItems()
    {
        if (_ids is { Count: > 0 }) return _ids;

        var ids = new List<string>();
        var seen = new HashSet<string>();
        var items = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
        if (items is not null)
            for (var i = 0; i < items.Count; i++)
            {
                var culture = items[i]?.Culture;
                if (culture is null || culture.IsBandit) continue;

                var id = culture.StringId;
                if (!string.IsNullOrEmpty(id) && seen.Add(id)) ids.Add(id);
            }

        // Cache only once the catalog is actually populated, so an early call
        // before load can't freeze an empty list for the session.
        if (ids.Count > 0) _ids = ids;
        return ids;
    }
}
