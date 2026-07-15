using System;
using System.Reflection;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.EquipBestItem.UI.Widgets;

/// <summary>
///     The priority rows list, with its own drop-index math.
///
///     The engine's Container.GetIndexForDrop multiplies row positions by
///     Context.CustomScale a second time, so at any UI scale other than 1 the
///     computed insertion index lags behind the cursor (confirmed in-game at
///     scale 1.33: the thresholds sat exactly at 1.33x the real row
///     boundaries). This widget therefore computes the index itself from the
///     hit-test rectangles (Widget.AreaRect — the same space mouse targeting
///     uses) and overrides OnDrop to fire the Drop event with the corrected
///     index; it also suppresses the engine's drag-hover insertion gap (which
///     both reflows the rows and uses the same broken index) and marks the
///     insertion point by lighting an edge line inside the target row.
///
///     Everything touching System.Numerics.Vector2 goes through reflection:
///     the mod compiles against the System.Numerics.Vectors package, whose
///     Vector2 is a different type identity than the game's — static member
///     references throw Missing(Method|Field)Exception at runtime.
/// </summary>
public sealed class EbiPriorityListWidget : ListPanel
{
    private const string TopLineId = "EbiRowInsertTop";
    private const string BottomLineId = "EbiRowInsertBottom";

    private static readonly PropertyInfo? MousePositionProperty =
        typeof(EventManager).GetProperty("MousePosition");

    private static readonly MethodInfo? ReleaseDraggedMethod =
        typeof(EventManager).GetMethod("ReleaseDraggedWidget",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo? AreaRectField = typeof(Widget).GetField("AreaRect");

    // Resolved lazily from the runtime types of the boxed values.
    private static FieldInfo? _rectTopLeftField;
    private static FieldInfo? _rectBottomLeftField;
    private static FieldInfo? _vectorYField;

    private bool _updateFailed;

    public EbiPriorityListWidget(UIContext context) : base(context)
    {
    }

    /// <summary>Always false: keeps StackLayout from opening the moving insertion gap.</summary>
    public override bool IsDragHovering => false;

    protected override void OnUpdate(float dt)
    {
        base.OnUpdate(dt);

        if (_updateFailed) return;

        try
        {
            UpdateInsertLines();
        }
        catch (Exception exception)
        {
            // Never take the game down over a cosmetic indicator.
            _updateFailed = true;
            GameLog.Warn($"Insertion marker disabled: {exception.Message}");
        }
    }

    /// <summary>
    ///     Replaces Container.OnDrop to report the corrected insertion index.
    ///     The prefab sets DropEventHandledManually, so like the stock path
    ///     with that flag, no widget reparenting happens here — the view
    ///     models rebuild the rows.
    /// </summary>
    protected override bool OnDrop()
    {
        if (!AcceptDrop || ReleaseDraggedMethod is null) return false;

        try
        {
            if (ReleaseDraggedMethod.Invoke(EventManager, null) is not Widget released) return false;

            EventFired("Drop", released, ComputeInsertIndex());
            return true;
        }
        catch (Exception exception)
        {
            GameLog.Warn($"Drop failed: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    ///     First row whose hit-rect middle lies below the cursor; past the
    ///     last row means "insert at the end". Uses the same rectangles as
    ///     mouse targeting, so it agrees with what the player points at.
    /// </summary>
    private int ComputeInsertIndex()
    {
        if (MousePositionProperty is null || AreaRectField is null) return ChildCount;

        var mouseY = VectorY(MousePositionProperty.GetValue(EventManager));

        for (var i = 0; i < ChildCount; i++)
        {
            var row = GetChild(i);
            if (row is null || !row.IsVisible) continue;

            var rect = AreaRectField.GetValue(row);
            if (rect is null) continue;

            _rectTopLeftField ??= rect.GetType().GetField("TopLeft");
            _rectBottomLeftField ??= rect.GetType().GetField("BottomLeft");
            if (_rectTopLeftField is null || _rectBottomLeftField is null) return ChildCount;

            var top = VectorY(_rectTopLeftField.GetValue(rect));
            var bottom = VectorY(_rectBottomLeftField.GetValue(rect));
            if (mouseY < (top + bottom) / 2f) return i;
        }

        return ChildCount;
    }

    private static float VectorY(object? boxedVector)
    {
        if (boxedVector is null) return 0f;

        _vectorYField ??= boxedVector.GetType().GetField("Y");
        return _vectorYField?.GetValue(boxedVector) is float y ? y : 0f;
    }

    private void UpdateInsertLines()
    {
        // The engine drag-hovers this list whenever a chip is dragged over it
        // but not over a row's link band — exactly when a drop would insert.
        var show = EventManager.DraggedWidget is not null &&
                   ReferenceEquals(EventManager.DragHoveredWidget, this);

        var index = show ? ComputeInsertIndex() : -1;

        for (var i = 0; i < ChildCount; i++)
        {
            var row = GetChild(i);
            if (row is null) continue;

            var topLine = row.FindChild(TopLineId);
            if (topLine is not null) topLine.IsVisible = show && index == i;

            // "Insert after the last row" lights the last row's bottom edge.
            var bottomLine = row.FindChild(BottomLineId);
            if (bottomLine is not null)
                bottomLine.IsVisible = show && index >= ChildCount && i == ChildCount - 1;
        }
    }
}
