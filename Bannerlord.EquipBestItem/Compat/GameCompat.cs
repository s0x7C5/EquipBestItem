using System;
using System.Reflection;
using Helpers;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.EquipBestItem.Compat;

/// <summary>
///     Shims for game members that do not exist on every supported game
///     version (floor: v1.3.5). Everything resolves once via reflection into a
///     cached delegate; a missing member degrades the feature instead of
///     throwing. See docs/COMPATIBILITY.md — members are probed, versions are
///     never compared, so future games keep taking the native path.
/// </summary>
internal static class GameCompat
{
    // CharacterHelper.CanUseItem appeared after v1.3.5. Prefer the native
    // helper (its rules can evolve with the game); fall back to a local port
    // of the 1.4.6 logic on games that predate it.
    private static readonly Func<BasicCharacterObject, EquipmentElement, bool> CanUseItemImpl =
        ResolveStatic<Func<BasicCharacterObject, EquipmentElement, bool>>(
            typeof(CharacterHelper), "CanUseItem") ?? CanUseItemFallback;

    // EventManager.HoveredWidget / DragHoveredWidget appeared after v1.3.5.
    // Both back cosmetic behavior only (Alt yielding to the native compare
    // cycle, drag highlights), so a null getter just switches it off.
    private static readonly Func<EventManager, Widget?>? HoveredWidgetGetter =
        ResolveGetter<EventManager, Widget?>("HoveredWidget");

    private static readonly Func<EventManager, Widget?>? DragHoveredWidgetGetter =
        ResolveGetter<EventManager, Widget?>("DragHoveredWidget");

    // GetModifiedStealthFactor arrived with the stealth equipment set. Where
    // it is missing the stat reads 0 and the popup drops its row, so nothing
    // offers a slider that could not move anything.
    private static readonly StealthFactorGetter? StealthFactorImpl = ResolveStealthFactor();

    /// <summary>An open instance delegate: a struct receiver is passed by reference.</summary>
    private delegate int StealthFactorGetter(ref EquipmentElement element);

    internal static bool SupportsStealth => StealthFactorImpl is not null;

    internal static bool CanUseItem(BasicCharacterObject character, EquipmentElement element) =>
        CanUseItemImpl(character, element);

    internal static Widget? GetHoveredWidget(EventManager eventManager) =>
        HoveredWidgetGetter?.Invoke(eventManager);

    internal static Widget? GetDragHoveredWidget(EventManager eventManager) =>
        DragHoveredWidgetGetter?.Invoke(eventManager);

    /// <summary>The item's stealth bonus with its modifier applied, or 0 where the game has no such stat.</summary>
    internal static int GetStealthFactor(EquipmentElement element) =>
        StealthFactorImpl is null ? 0 : StealthFactorImpl(ref element);

    private static StealthFactorGetter? ResolveStealthFactor()
    {
        var method = typeof(EquipmentElement).GetMethod(
            "GetModifiedStealthFactor", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        return method is null
            ? null
            : Delegate.CreateDelegate(typeof(StealthFactorGetter), method, false) as StealthFactorGetter;
    }

    private static TDelegate? ResolveStatic<TDelegate>(Type type, string name) where TDelegate : class
    {
        var parameters = typeof(TDelegate).GetMethod("Invoke")!.GetParameters();
        var parameterTypes = Array.ConvertAll(parameters, parameter => parameter.ParameterType);
        var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, parameterTypes, null);
        return method is null ? null : Delegate.CreateDelegate(typeof(TDelegate), method, false) as TDelegate;
    }

    private static Func<TInstance, TValue>? ResolveGetter<TInstance, TValue>(string propertyName)
    {
        var getter = typeof(TInstance).GetProperty(propertyName)?.GetGetMethod();
        return getter is null
            ? null
            : Delegate.CreateDelegate(typeof(Func<TInstance, TValue>), getter, false) as Func<TInstance, TValue>;
    }

    /// <summary>Port of CharacterHelper.CanUseItem as of game v1.4.6.</summary>
    private static bool CanUseItemFallback(BasicCharacterObject character, EquipmentElement element)
    {
        var item = element.Item;

        var relevantSkill = item.RelevantSkill;
        if (relevantSkill is not null && character.GetSkillValue(relevantSkill) < item.Difficulty) return false;

        var blockedBySex = character.IsFemale ? ItemFlags.NotUsableByFemale : ItemFlags.NotUsableByMale;
        if ((item.ItemFlags & blockedBySex) != 0) return false;

        if (item.StringId is "dragon_banner_center" or "dragon_banner_dragonhead" or "dragon_banner_handle")
            return false;

        return !item.HasHorseComponent || item.HorseComponent.IsRideable;
    }
}
