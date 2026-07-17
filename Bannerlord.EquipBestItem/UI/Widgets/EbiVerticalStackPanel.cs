using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Layout;

namespace Bannerlord.EquipBestItem.UI.Widgets;

/// <summary>
///     A ListPanel that stacks children top-down on every supported game
///     version. Prefab XML resolves LayoutMethod by NAME at runtime, and
///     TaleWorlds swapped the names of the vertical members after v1.3 while
///     keeping the behavior of the numeric slots (slot 4 lays children
///     top-down everywhere: it is called VerticalBottomToTop before 1.4 and
///     VerticalTopToBottom after). An enum constant compiled against the
///     current references bakes in the number, so setting it from code is
///     version-proof — a StackLayout.LayoutMethod attribute in the prefab
///     would flip direction depending on the game. Keep that attribute out
///     of the XML for every vertical stack.
/// </summary>
public class EbiVerticalStackPanel : ListPanel
{
    public EbiVerticalStackPanel(UIContext context) : base(context)
    {
        StackLayout.LayoutMethod = LayoutMethod.VerticalTopToBottom;
    }
}
