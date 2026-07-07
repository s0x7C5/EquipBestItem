using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.EquipBestItem.UI.Patches;

/// <summary>
///     Mounts the (initially hidden) slot weights popup as the last child of
///     the inventory screen so it overlays everything when opened.
/// </summary>
[PrefabExtension("Inventory", "descendant::InventoryScreenWidget/Children")]
public sealed class WeightsPopupPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    /// <summary>After the input key visual, before the native tooltip and preview overlays.</summary>
    public override int Index => 3;

    [PrefabExtensionFileName]
    public string File => "EbiWeightsPopupHost";
}
