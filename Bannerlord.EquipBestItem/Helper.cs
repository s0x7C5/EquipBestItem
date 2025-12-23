using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem;

internal static class Helper
{
    internal static void ShowMessage(string text, Color? color = null)
    {
        InformationManager.DisplayMessage(new InformationMessage($"{text}"));
    }
}