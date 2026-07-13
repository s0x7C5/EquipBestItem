using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.EquipBestItem.UI.Widgets;

/// <summary>
///     The AI prompt input. Grabs keyboard focus once each time it becomes
///     visible, so the player can type immediately — and while it has focus
///     the inventory screen skips its own hotkeys (the same guard the native
///     inventory search relies on). "Just became visible" is detected both by
///     the visibility flag and by a gap in update time, since hidden widgets
///     may or may not keep receiving updates.
/// </summary>
public sealed class EbiPromptInputWidget : EditableTextWidget
{
    private const float ReopenGapSeconds = 0.25f;

    private bool _focusRequested;
    private float _lastUpdateTime;

    public EbiPromptInputWidget(UIContext context) : base(context)
    {
    }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);

        if (!IsVisible)
        {
            _focusRequested = false;
            return;
        }

        var now = EventManager.Time;
        if (now - _lastUpdateTime > ReopenGapSeconds) _focusRequested = false;
        _lastUpdateTime = now;

        if (_focusRequested) return;

        _focusRequested = true;
        EventManager.FocusedWidget = this;
    }
}
