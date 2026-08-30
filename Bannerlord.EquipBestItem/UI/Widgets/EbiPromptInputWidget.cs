using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.EquipBestItem.UI.Widgets;

/// <summary>
///     The AI prompt input. Grabs keyboard focus once each time it becomes
///     visible, so the player can type immediately — and while it has focus
///     the inventory screen skips its own hotkeys (the same guard the native
///     inventory search relies on).
///
///     Visibility must be checked recursively: the prefab toggles an ancestor
///     container, so this widget's own IsVisible stays true forever — and the
///     engine keeps calling custom OnLateUpdate overrides regardless of
///     visibility. Checking the own flag here once made the hidden field grab
///     focus the moment the inventory opened, which silenced every screen
///     hotkey (including the Alt compare cycle) until the next click.
/// </summary>
public sealed class EbiPromptInputWidget : EditableTextWidget
{
    private bool _focusRequested;

    public EbiPromptInputWidget(UIContext context) : base(context)
    {
    }

    protected override void OnLateUpdate(float dt)
    {
        base.OnLateUpdate(dt);

        if (!IsRecursivelyVisible())
        {
            _focusRequested = false;
            // A hidden widget keeping keyboard focus would silence the
            // inventory hotkeys indefinitely (the screen skips them while
            // any text widget in the layer is focused).
            if (ReferenceEquals(EventManager.FocusedWidget, this))
                EventManager.FocusedWidget = null;
            return;
        }

        if (_focusRequested) return;

        _focusRequested = true;
        EventManager.FocusedWidget = this;
    }
}
