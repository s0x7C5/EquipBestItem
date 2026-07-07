using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.EquipBestItem.UI.Patches;

/// <summary>
///     Adds the "equip best" and "search weights" buttons to every equipped
///     item slot. The native slot prefab is parameterized (one file serves all
///     twelve slots), so a single patch with a static XML snippet covers them
///     all — the snippet reads the slot identity from the *DropTag parameter.
/// </summary>
[PrefabExtension("InventoryEquippedItemSlot", "/Prefab/Window/Widget/Children")]
public sealed class EquippedItemSlotPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    /// <summary>Append after the native children so the buttons draw on top.</summary>
    public override int Index => 4;

    [PrefabExtensionFileName]
    public string File => "EbiSlotButtons";
}
