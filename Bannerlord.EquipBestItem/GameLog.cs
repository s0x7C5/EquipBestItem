using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem;

/// <summary>In-game message log.</summary>
internal static class GameLog
{
    internal static void Info(string text) =>
        InformationManager.DisplayMessage(new InformationMessage(text));

    /// <summary>AI assistant output — a distinct colour so it reads as the assistant talking.</summary>
    internal static void Ai(string text) =>
        InformationManager.DisplayMessage(new InformationMessage(text, new Color(0.55f, 0.8f, 1f)));

    internal static void Warn(string text) =>
        InformationManager.DisplayMessage(new InformationMessage($"EquipBestItem: {text}", Colors.Yellow));

    internal static void Error(string text) =>
        InformationManager.DisplayMessage(new InformationMessage($"EquipBestItem: {text}", Colors.Red));
}
