using TaleWorlds.GauntletUI;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Inventory;

namespace Bannerlord.EquipBestItem.UI.Widgets;

/// <summary>
///     Hover target for the native item comparison tooltip. The inventory
///     screen shows the tooltip for whatever SPItemVM is the DataSource of the
///     nearest <see cref="InventoryItemButtonWidget" /> ancestor of the hovered
///     widget — so binding this wrapper to the found item previews it.
/// </summary>
public sealed class EbiFoundItemTooltipWidget : InventoryItemButtonWidget
{
    public EbiFoundItemTooltipWidget(UIContext context) : base(context)
    {
    }
}
