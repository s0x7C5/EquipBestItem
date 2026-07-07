using System.Collections.Generic;

namespace Bannerlord.EquipBestItem.Ai;

/// <summary>
///     A plain-data snapshot of the situation the request refers to. Captured
///     on the main thread, consumed on a background thread — so no game objects.
/// </summary>
public sealed class InterpretationContext
{
    public InterpretationContext(string characterName, string equipmentSetKey, IReadOnlyList<string> notableSkills)
    {
        CharacterName = characterName;
        EquipmentSetKey = equipmentSetKey;
        NotableSkills = notableSkills;
    }

    public string CharacterName { get; }

    /// <summary>"battle", "civilian" or "stealth".</summary>
    public string EquipmentSetKey { get; }

    /// <summary>E.g. "One Handed: 130". Helps the model pick weapon classes.</summary>
    public IReadOnlyList<string> NotableSkills { get; }
}
