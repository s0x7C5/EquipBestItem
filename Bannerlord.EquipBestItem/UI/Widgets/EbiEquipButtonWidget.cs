using Bannerlord.EquipBestItem.Compat;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Inventory;

namespace Bannerlord.EquipBestItem.UI.Widgets;

/// <summary>
///     Slot button: hidden (via the brush's Disabled style) when no better
///     item exists. Holding Left Alt temporarily reveals hidden buttons so the
///     search settings stay reachable through right click; left click while
///     Alt is held is suppressed so nothing gets equipped accidentally.
///
///     Left Alt is also the game's SwitchAlternative hotkey — while an item
///     comparison tooltip is up it cycles the compared item. The two uses are
///     never needed at once, so the reveal yields: it only engages when the
///     cursor is not on an item with a comparison (a list row or this mod's
///     preview button). Hovering an equipped slot still reveals — the native
///     cycle is inert there, and Alt+RMB on a slot is the reveal's whole point.
/// </summary>
public sealed class EbiEquipButtonWidget : ButtonWidget
{
    private bool _stateBeforeAltReveal;
    private bool _altRevealActive;
    private Color _buttonColor = Color.White;

    public EbiEquipButtonWidget(UIContext context) : base(context)
    {
    }

    /// <summary>
    ///     Player-configured tint. Brush-based widgets ignore the plain Color
    ///     attribute, so the tint goes through a per-widget brush clone's
    ///     GlobalColor, which multiplies every state (default/hover/pressed).
    /// </summary>
    [Editor(false)]
    public Color ButtonColor
    {
        get => _buttonColor;
        set
        {
            if (value == _buttonColor) return;
            _buttonColor = value;
            OnPropertyChanged(value, nameof(ButtonColor));

            Brush = Brush.Clone();
            Brush.GlobalColor = value;
        }
    }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);

        if (Input.IsKeyPressed(InputKey.LeftAlt) && !IsComparisonHovered())
        {
            _stateBeforeAltReveal = IsDisabled;
            _altRevealActive = true;
            IsDisabled = false;
        }

        // Restore only what this widget changed: when the press was yielded
        // to the native compare cycle, a stale restore would overwrite the
        // view model's binding.
        if (Input.IsKeyReleased(InputKey.LeftAlt) && _altRevealActive)
        {
            _altRevealActive = false;
            IsDisabled = _stateBeforeAltReveal;
        }
    }

    /// <summary>
    ///     True while the cursor is on an item whose tooltip offers the native
    ///     Alt comparison cycle: any item button except an equipped slot (the
    ///     cycle only runs for non-equipment-side items).
    /// </summary>
    private bool IsComparisonHovered()
    {
        for (var widget = GameCompat.GetHoveredWidget(EventManager); widget is not null; widget = widget.ParentWidget)
        {
            if (widget is InventoryEquippedItemSlotWidget) return false;
            if (widget is InventoryItemButtonWidget) return true;
        }

        return false;
    }

    protected override void OnMousePressed()
    {
        if (Input.IsKeyDown(InputKey.LeftAlt)) return;

        base.OnMousePressed();
    }

    // Not OnMouseReleased(bool): that virtual gained a parameter after game
    // v1.3.5, and an override binds to one slot only — the type would fail to
    // load on older games. Guarding HandleClick suppresses the click on every
    // path while base release keeps the button's press state consistent.
    protected override void HandleClick()
    {
        if (Input.IsKeyDown(InputKey.LeftAlt)) return;

        base.HandleClick();
    }
}
