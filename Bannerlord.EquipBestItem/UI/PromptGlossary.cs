using System;
using System.Text;
using Bannerlord.EquipBestItem.Domain;
using Bannerlord.EquipBestItem.UI.ViewModels;
using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.UI;

/// <summary>
///     Builds the prompt glossary mapping the player's game-language terms to
///     the JSON identifiers the model must emit. The game's own localized
///     strings are the dictionary, so every language the game ships works
///     without hand-maintained word lists. Built lazily on the main thread
///     (first AI request) and cached for the session.
/// </summary>
internal static class PromptGlossary
{
    private static readonly Lazy<string> Cached = new(Build);

    internal static string Text => Cached.Value;

    private static string Build()
    {
        // An English prompt needs no glossary: the identifiers ARE English.
        if (SlotWeightsVM.GetParamName(ItemParam.HeadArmor) == "Head Armor")
            return "";

        var builder = new StringBuilder();
        builder.Append("Glossary of the player's game language, \"term\" = identifier.\nParams: ");

        for (var i = 0; i < ItemParams.Count; i++)
        {
            var param = (ItemParam)i;
            if (i > 0) builder.Append(", ");
            builder.Append('"').Append(SlotWeightsVM.GetParamName(param)).Append("\" = ").Append(param);
        }

        builder.Append(".\nWeapon classes: ");
        var first = true;
        foreach (var category in SlotWeightsVM.PinnableWeaponCategories)
        {
            if (!first) builder.Append(", ");
            first = false;
            builder.Append('"')
                .Append(SlotWeightsVM.GetCategoryName(category))
                .Append("\" = ").Append(category);
        }

        builder.Append(".\nSlots: ");
        var slots = new[]
        {
            EquipmentIndex.Head, EquipmentIndex.Cape, EquipmentIndex.Body,
            EquipmentIndex.Gloves, EquipmentIndex.Leg, EquipmentIndex.Horse,
            EquipmentIndex.HorseHarness
        };
        for (var i = 0; i < slots.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append('"').Append(SlotWeightsVM.GetSlotName(slots[i])).Append("\" = ").Append(slots[i]);
        }

        builder.Append('.');
        return builder.ToString();
    }
}
