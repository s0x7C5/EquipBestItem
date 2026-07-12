using System.Collections.Generic;

namespace Bannerlord.EquipBestItem.Ai;

/// <summary>
///     A plain-data snapshot of the situation the request refers to. Captured
///     on the main thread, consumed on a background thread — so no game objects.
/// </summary>
public sealed class InterpretationContext
{
    public InterpretationContext(
        string characterName, string equipmentSetKey, IReadOnlyList<string> notableSkills,
        string languageGlossary = "", IReadOnlyList<string>? partyHeroes = null,
        string gameLanguage = "English", string searchMethod = "weights")
    {
        CharacterName = characterName;
        EquipmentSetKey = equipmentSetKey;
        NotableSkills = notableSkills;
        LanguageGlossary = languageGlossary;
        PartyHeroes = partyHeroes ?? System.Array.Empty<string>();
        GameLanguage = gameLanguage;
        SearchMethod = searchMethod;
    }

    public string CharacterName { get; }

    /// <summary>"battle", "civilian" or "stealth".</summary>
    public string EquipmentSetKey { get; }

    /// <summary>E.g. "One Handed: 130". Helps the model pick weapon classes.</summary>
    public IReadOnlyList<string> NotableSkills { get; }

    /// <summary>
    ///     "term = identifier" lines in the game's UI language, built from the
    ///     game's own localized strings. Empty when the game runs in English.
    /// </summary>
    public string LanguageGlossary { get; }

    /// <summary>
    ///     Equippable party hero names, the main hero marked with "(main)".
    ///     Lets the model resolve targets like "everyone except me".
    /// </summary>
    public IReadOnlyList<string> PartyHeroes { get; }

    /// <summary>
    ///     The game's UI language name (e.g. "Русский"). The explanation is
    ///     requested in this language so the status line stays one language,
    ///     whatever language the player typed in.
    /// </summary>
    public string GameLanguage { get; }

    /// <summary>
    ///     The player's active search method — "weights", "priority" or
    ///     "effectiveness" — so the model expresses stat preferences in the
    ///     form the search will actually use.
    /// </summary>
    public string SearchMethod { get; }
}
