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

    public override int Index => 1;

    [PrefabExtensionFileName]
    public string File => "EbiCenterPanel";
}
