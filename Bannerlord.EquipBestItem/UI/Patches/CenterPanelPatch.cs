using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.EquipBestItem.UI.Patches;

/// <summary>
///     Inserts the mod panel (AI request box + equip-all button) into the
///     center panel of the inventory screen.
/// </summary>
[PrefabExtension("Inventory", "descendant::InventoryCenterPanelWidget[@Id='CenterPanel']/Children")]
public sealed class CenterPanelPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    /// <summary>
    ///     Appended after the native children: later siblings render on top and
    ///     receive input first, otherwise the panel buttons are unclickable.
    /// </summary>
    public override int Index => 6;

    [PrefabExtensionFileName]
    public string File => "EbiCenterPanel";
}
