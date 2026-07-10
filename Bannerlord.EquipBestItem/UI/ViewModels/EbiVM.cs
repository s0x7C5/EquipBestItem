using Bannerlord.EquipBestItem.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.EquipBestItem.UI.ViewModels;

/// <summary>
///     Root view model of the mod, exposed on the inventory screen as
///     "ModInventory". Holds one <see cref="EbiSlotVM" /> per searchable slot;
///     best items are recomputed synchronously on inventory change events —
///     the cached single-pass search is fast enough to not need the async
///     update dance the legacy version had.
/// </summary>
public sealed class EbiVM : ViewModel
{
    private readonly ModServices _services;
    private readonly InventoryGateway _gateway;
    private readonly EbiSlotVM[] _slots;

    internal EbiVM(ModServices services, InventoryGateway gateway)
    {
        _services = services;
        _gateway = gateway;

        // Recompute the slot buttons on every weights change: the player has
        // just changed what "best" means, so the previews follow live.
        SlotSettings = new SlotWeightsVM(services.Profiles, RecomputeBestItems);
        Ai = new AiPromptVM(
            services.Interpreter, services.EquipBest, gateway,
            services.Settings.Ai.IsConfigured);

        EbiSlotVM Create(EquipmentIndex slot) => new(slot, EquipFound, OpenSettings);

        SlotWeapon0 = Create(EquipmentIndex.Weapon0);
        SlotWeapon1 = Create(EquipmentIndex.Weapon1);
        SlotWeapon2 = Create(EquipmentIndex.Weapon2);
        SlotWeapon3 = Create(EquipmentIndex.Weapon3);
        SlotHead = Create(EquipmentIndex.Head);
        SlotCape = Create(EquipmentIndex.Cape);
        SlotBody = Create(EquipmentIndex.Body);
        SlotGloves = Create(EquipmentIndex.Gloves);
        SlotLeg = Create(EquipmentIndex.Leg);
        SlotHorse = Create(EquipmentIndex.Horse);
        SlotHarness = Create(EquipmentIndex.HorseHarness);

        _slots = new[]
        {
            SlotWeapon0, SlotWeapon1, SlotWeapon2, SlotWeapon3,
            SlotHead, SlotCape, SlotBody, SlotGloves, SlotLeg,
            SlotHorse, SlotHarness
        };
    }

    [DataSourceProperty]
    public AiPromptVM Ai { get; }

    [DataSourceProperty]
    public SlotWeightsVM SlotSettings { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotWeapon0 { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotWeapon1 { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotWeapon2 { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotWeapon3 { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotHead { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotCape { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotBody { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotGloves { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotLeg { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotHorse { get; }

    [DataSourceProperty]
    public EbiSlotVM SlotHarness { get; }

    [DataSourceProperty]
    public HintViewModel EquipCurrentHint { get; } = new(new TextObject(
        "{=EbiHintEquipCurrent}Equip the current character with the best items"));

    [DataSourceProperty]
    public HintViewModel EquipAllHint { get; } = new(new TextObject(
        "{=EbiHintEquipAll}Equip all party heroes with the best items"));

    [DataSourceProperty]
    public HintViewModel LeftPanelLockHint { get; } = new(new TextObject(
        "{=EbiHintLeftLock}Search in the left panel (merchant, loot). Equipping buys the item."));

    [DataSourceProperty]
    public HintViewModel RightPanelLockHint { get; } = new(new TextObject(
        "{=EbiHintRightLock}Search in your inventory"));

    [DataSourceProperty]
    public bool IsLeftPanelSearched => _services.Settings.SearchLeftPanel;

    [DataSourceProperty]
    public bool IsLeftPanelLocked => !_services.Settings.SearchLeftPanel;

    [DataSourceProperty]
    public bool IsRightPanelSearched => _services.Settings.SearchRightPanel;

    [DataSourceProperty]
    public bool IsRightPanelLocked => !_services.Settings.SearchRightPanel;

    public void ExecuteToggleLeftPanelSearch()
    {
        _services.Settings.SearchLeftPanel = !_services.Settings.SearchLeftPanel;
        _services.PersistSettings();
        OnPropertyChanged(nameof(IsLeftPanelSearched));
        OnPropertyChanged(nameof(IsLeftPanelLocked));
        RecomputeBestItems();
    }

    public void ExecuteToggleRightPanelSearch()
    {
        _services.Settings.SearchRightPanel = !_services.Settings.SearchRightPanel;
        _services.PersistSettings();
        OnPropertyChanged(nameof(IsRightPanelSearched));
        OnPropertyChanged(nameof(IsRightPanelLocked));
        RecomputeBestItems();
    }

    public void ExecuteEquipAllBest()
    {
        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;
        if (character is null || equipment is null) return;

        // Re-search sequentially instead of equipping the previews: each equip
        // changes the inventory, and two slots may want the same single item.
        var equippedCount = 0;
        foreach (var slot in _slots)
        {
            var query = _services.Profiles.GetQuery(character, equipment, slot.Slot);
            if (_services.EquipBest.TryEquipBest(_gateway, query, slot.Slot) is not null)
                equippedCount++;
        }

        GameLog.Info(new TextObject("{=EbiEquippedCount}Equipped {COUNT} item(s).")
            .SetTextVariable("COUNT", equippedCount).ToString());
    }

    public void ExecuteEquipAllCharacters()
    {
        var equippedCount = 0;
        foreach (var character in _gateway.GetEquippableHeroes())
        {
            var equipment = _gateway.GetEquipmentFor(character);
            if (equipment is null) continue;

            foreach (var slot in _slots)
            {
                var query = _services.Profiles.GetQuery(character, equipment, slot.Slot);
                if (_services.EquipBest.TryEquipBest(_gateway, query, slot.Slot, character, equipment) is not null)
                    equippedCount++;
            }
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

        RecomputeBestItems();
    }

    public override void OnFinalize()
    {
        _services.Profiles.Save();
        Ai.OnFinalize();
        base.OnFinalize();
    }

    private void RecomputeBestItems()
    {
        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;

        foreach (var slot in _slots)
        {
            if (character is null || equipment is null)
            {
                slot.SetBest(null, null);
                continue;
            }

            var query = _services.Profiles.GetQuery(character, equipment, slot.Slot);
            slot.SetBest(
                _services.EquipBest.FindBest(_gateway, query, slot.Slot),
                _gateway.GetEquippedItemVM(slot.Slot));
        }
    }

    private void EquipFound(EbiSlotVM slot)
    {
        if (slot.FoundItem is null) return;

        _services.EquipBest.Equip(_gateway, slot.FoundItem, slot.Slot);
        // The transfer fires InventoryLogic.AfterTransfer, which recomputes buttons.
    }

    private void OpenSettings(EquipmentIndex slot)
    {
        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;
        if (character is null || equipment is null) return;

        SlotSettings.Open(character, equipment, slot);
    }
}
