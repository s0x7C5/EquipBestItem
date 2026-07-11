using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem.UI.Widgets;

/// <summary>
///     Slot button: hidden (via the brush's Disabled style) when no better
///     item exists. Holding Left Alt temporarily reveals hidden buttons so the
///     search settings stay reachable through right click; left click while
///     Alt is held is suppressed so nothing gets equipped accidentally.
/// </summary>
public sealed class EbiEquipButtonWidget : ButtonWidget
{
    private bool _stateBeforeAltReveal;
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

        if (Input.IsKeyPressed(InputKey.LeftAlt))
        {
            _stateBeforeAltReveal = IsDisabled;
            IsDisabled = false;
        }

        if (Input.IsKeyReleased(InputKey.LeftAlt))
            IsDisabled = _stateBeforeAltReveal;
    }

    protected override void OnMousePressed()
    {
        if (Input.IsKeyDown(InputKey.LeftAlt)) return;

        base.OnMousePressed();
    }

    protected override void OnMouseReleased(bool isCancel)
    {
        if (Input.IsKeyDown(InputKey.LeftAlt)) return;

        base.OnMouseReleased(isCancel);
    }
}
