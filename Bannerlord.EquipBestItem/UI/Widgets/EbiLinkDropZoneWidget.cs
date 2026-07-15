using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.EquipBestItem.UI.Widgets;

/// <summary>
///     The "link with this rank" drop band in the priority popup. Drives the
///     row highlight from the frame update by checking whether the engine
///     currently drag-hovers this band (EventManager.DragHoveredWidget), and
///     toggles the sibling glow widget, looked up lazily by id.
///
///     Deliberately avoids every API with System.Numerics.Vector2 in its
///     signature (EventManager.MousePosition, Widget.GlobalPosition/Size,
///     IsPointInsideMeasuredArea): the mod compiles against the
///     System.Numerics.Vectors package (needed for TaleWorlds Color), so its
///     Vector2 is a DIFFERENT type identity than the game's framework
///     System.Numerics — calling such members throws MissingMethodException
///     at runtime. Command bindings for DragHoverBegin/End never fired at
///     all, hence the per-frame check. Dropping is the stock container
///     behavior (AcceptDrop + Command.Drop in the prefab).
/// </summary>
public sealed class EbiLinkDropZoneWidget : ListPanel
{
    private const string GlowWidgetId = "LinkGlow";

    private Widget? _glow;
    private bool _glowSearched;
    private bool _updateFailed;

    public EbiLinkDropZoneWidget(UIContext context) : base(context)
    {
    }

    protected override void OnUpdate(float dt)
    {
        base.OnUpdate(dt);

        if (_updateFailed) return;

        try
        {
            if (!_glowSearched && ParentWidget is not null)
            {
                _glow = ParentWidget.FindChild(GlowWidgetId);
                _glowSearched = true;
            }

            if (_glow is null) return;

            _glow.IsVisible = EventManager.DraggedWidget is not null &&
                              ReferenceEquals(EventManager.DragHoveredWidget, this);
        }
        catch (Exception exception)
        {
            // Never take the game down over a cosmetic highlight.
            _updateFailed = true;
            GameLog.Warn($"Link drop zone highlight disabled: {exception.Message}");
        }
    }
}
