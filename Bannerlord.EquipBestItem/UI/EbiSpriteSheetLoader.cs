using System;
using System.IO;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ModuleManager;

namespace Bannerlord.EquipBestItem.UI;

/// <summary>
///     Feeds the mod's sprite-sheet texture to the UI at runtime. The engine
///     only looks textures up in its compiled asset registry (tpac +
///     RuntimeDataCache), which takes the official asset packer to rebuild;
///     loading the sheet PNG ourselves and swapping it into the loaded sprite
///     category keeps icon changes a plain file edit. Idempotent and cheap, so
///     it is re-asserted on every inventory open: a resolution change or a UI
///     resource refresh rebuilds the category with a texture we did not set.
/// </summary>
internal static class EbiSpriteSheetLoader
{
    private const string CategoryName = "ui_equipbestitem";
    private const string SheetFileName = "ui_equipbestitem_1.png";

    private static TaleWorlds.TwoDimension.Texture? _sheet;
    private static bool _loadFailed;

    public static void EnsureLoaded()
    {
        try
        {
            if (_loadFailed || UIResourceManager.SpriteData is null) return;

            var category = UIResourceManager.GetSpriteCategory(CategoryName);
            if (category is null || !category.IsLoaded) return;
            if (category.SpriteSheets.Count > 0 && _sheet is not null &&
                ReferenceEquals(category.SpriteSheets[0], _sheet))
                return;

            _sheet ??= LoadSheet();
            if (_sheet is null)
            {
                _loadFailed = true;
                return;
            }

            if (category.SpriteSheets.Count > 0) category.SpriteSheets[0] = _sheet;
            else category.SpriteSheets.Add(_sheet);
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            GameLog.Warn($"sprite sheet load failed: {exception.Message}");
        }
    }

    private static TaleWorlds.TwoDimension.Texture? LoadSheet()
    {
        var folder = Path.Combine(ModuleHelper.GetModuleFullPath("Bannerlord.EquipBestItem"),
            "GUI", "SpriteSheets", CategoryName) + Path.DirectorySeparatorChar;
        if (!File.Exists(folder + SheetFileName))
        {
            GameLog.Warn($"sprite sheet missing: {folder}{SheetFileName}");
            return null;
        }

        var engineTexture = TaleWorlds.Engine.Texture.LoadTextureFromPath(SheetFileName, folder);
        if (engineTexture is null) return null;
        return new TaleWorlds.TwoDimension.Texture(new EngineTexture(engineTexture));
    }
}
