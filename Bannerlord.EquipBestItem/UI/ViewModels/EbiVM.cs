using System.Globalization;
using Bannerlord.EquipBestItem.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.EquipBestItem.UI.ViewModels;

/// <summary>
///     Root view model of the mod, exposed on the inventory screen as
///     "ModInventory". Slot buttons identify themselves with the native
///     drop tags ("Equipment_5"), so one prefab patch serves all slots.
/// </summary>
public sealed class EbiVM : ViewModel
{
    private static readonly EquipmentIndex[] SearchableSlots =
    {
        EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3,
        EquipmentIndex.Head, EquipmentIndex.Cape, EquipmentIndex.Body, EquipmentIndex.Gloves, EquipmentIndex.Leg,
        EquipmentIndex.Horse, EquipmentIndex.HorseHarness
    };

    private readonly ModServices _services;
    private readonly InventoryGateway _gateway;

    internal EbiVM(ModServices services, InventoryGateway gateway)
    {
        _services = services;
        _gateway = gateway;

        SlotSettings = new SlotWeightsVM(services.Profiles);
        Ai = new AiPromptVM(
            services.Interpreter, services.EquipBest, gateway,
            services.Settings.Ai.IsConfigured);
    }

    [DataSourceProperty]
    public AiPromptVM Ai { get; }

    [DataSourceProperty]
    public SlotWeightsVM SlotSettings { get; }

    public void ExecuteEquipBest(string dropTag)
    {
        if (!TryParseSlot(dropTag, out var slot)) return;

        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;
        if (character is null || equipment is null) return;

        var query = _services.Profiles.GetQuery(character, equipment, slot);
        var equippedName = _services.EquipBest.TryEquipBest(_gateway, query, slot);

        if (equippedName is null)
            GameLog.Info(new TextObject("{=EbiNothingBetter}No better item found.").ToString());
    }

    public void ExecuteOpenSlotSettings(string dropTag)
    {
        if (!TryParseSlot(dropTag, out var slot)) return;
        if (slot == EquipmentIndex.ExtraWeaponSlot) return;

        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;
        if (character is null || equipment is null) return;

        SlotSettings.Open(character, equipment, slot);
    }

    public void ExecuteEquipAllBest()
    {
        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;
        if (character is null || equipment is null) return;

        var equippedCount = 0;
        foreach (var slot in SearchableSlots)
        {
            var query = _services.Profiles.GetQuery(character, equipment, slot);
            if (_services.EquipBest.TryEquipBest(_gateway, query, slot) is not null)
                equippedCount++;
        }

        GameLog.Info(new TextObject("{=EbiEquippedCount}Equipped {COUNT} item(s).")
            .SetTextVariable("COUNT", equippedCount).ToString());
    }

    /// <summary>Called when inventory contents or the shown character change.</summary>
    internal void OnInventoryChanged()
    {
        _services.EquipBest.InvalidateCaches();

        if (SlotSettings.IsVisible)
            SlotSettings.ExecuteClose();
    }

    public override void OnFinalize()
    {
        _services.Profiles.Save();
        Ai.OnFinalize();
        base.OnFinalize();
    }

    private static bool TryParseSlot(string dropTag, out EquipmentIndex slot)
    {
        slot = EquipmentIndex.None;

        const string prefix = "Equipment_";
        if (dropTag is null || !dropTag.StartsWith(prefix)) return false;

        if (!int.TryParse(dropTag.Substring(prefix.Length), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var index))
            return false;

        slot = (EquipmentIndex)index;
        return true;
    }
}
