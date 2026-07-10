using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.EquipBestItem.UI.Patches;

/// <summary>
///     Adds the "equip best" button (with the found-item tooltip) to every
///     equipped item slot. Each slot needs its own patch because the button
///     binds a per-slot view model (its found item feeds the native tooltip),
///     and Gauntlet bindings cannot be parameterized — the eleven snippets
///     differ only in the bound property name. The banner slot is skipped:
///     banners have no scorable stats.
/// </summary>
internal static class EquippedSlotPatches
{
    public abstract class SlotPatchBase : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_0']")]
    public sealed class Weapon0 : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Weapon0";
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_1']")]
    public sealed class Weapon1 : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Weapon1";
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_2']")]
    public sealed class Weapon2 : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Weapon2";
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_3']")]
    public sealed class Weapon3 : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Weapon3";
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_5']")]
    public sealed class Head : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Head";
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_6']")]
    public sealed class Body : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Body";
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_7']")]
    public sealed class Leg : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Leg";
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_8']")]
    public sealed class Gloves : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Gloves";
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_9']")]
    public sealed class Cape : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Cape";
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_10']")]
    public sealed class Horse : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Horse";
    }

    [PrefabExtension("Inventory", "descendant::InventoryEquippedItemSlot[@Parameter.DropTag='Equipment_11']")]
    public sealed class Harness : SlotPatchBase
    {
        [PrefabExtensionFileName]
        public string File => "EbiSlot_Harness";
    }
}
