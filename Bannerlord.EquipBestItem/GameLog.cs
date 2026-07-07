using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem;

/// <summary>In-game message log.</summary>
internal static class GameLog
{
    internal static void Info(string text) =>
        InformationManager.DisplayMessage(new InformationMessage(text));

    internal static void Warn(string text) =>
        InformationManager.DisplayMessage(new InformationMessage($"EquipBestItem: {text}", Colors.Yellow));

    internal static void Error(string text) =>
        InformationManager.DisplayMessage(new InformationMessage($"EquipBestItem: {text}", Colors.Red));
}
