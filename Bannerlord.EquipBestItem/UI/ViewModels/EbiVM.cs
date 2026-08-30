using System.Collections.Generic;
using Bannerlord.EquipBestItem.Domain.Explaining;
using Bannerlord.EquipBestItem.Inventory;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
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
        SlotSettings = new SlotWeightsVM(services.Profiles, services.Settings, RecomputeBestItems, ExplainSlot);
        Ai = new AiPromptVM(
            services.Interpreter, services.Profiles, gateway,
            services.Settings, RecomputeBestItems, CollectSlotExplanations, ExplainNamedItem);

        var buttonColor = ParseColor(services.Settings.SlotButtonColor);
        EbiSlotVM Create(EquipmentIndex slot) => new(slot, EquipFound, OpenSettings, buttonColor);

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

    /// <summary>
    ///     The right plaque's background fits three sockets; without an AI
    ///     backend only two are used, so the plaque slides right by one
    ///     socket-width — mirroring the left plaque's fixed shift. With the
    ///     AI socket shown, an extra 8px tucks the wider plaque flush against
    ///     the panel edge.
    /// </summary>
    [DataSourceProperty]
    public float RightPlaqueOffset => _services.Settings.Ai.IsConfigured ? 8f : 43f;

    [DataSourceProperty]
    public bool IsLeftPanelSearched => _services.Settings.SearchLeftPanel;

    [DataSourceProperty]
    public bool IsLeftPanelLocked => !_services.Settings.SearchLeftPanel;

    [DataSourceProperty]
    public bool IsRightPanelSearched => _services.Settings.SearchRightPanel;

    [DataSourceProperty]
    public bool IsRightPanelLocked => !_services.Settings.SearchRightPanel;

    public void ExecuteToggleLeftPanelSearch() => GameLog.Guard("panel toggle", () =>
    {
        _services.Settings.SearchLeftPanel = !_services.Settings.SearchLeftPanel;
        _services.PersistSettings();
        OnPropertyChanged(nameof(IsLeftPanelSearched));
        OnPropertyChanged(nameof(IsLeftPanelLocked));
        RecomputeBestItems();
    });

    public void ExecuteToggleRightPanelSearch() => GameLog.Guard("panel toggle", () =>
    {
        _services.Settings.SearchRightPanel = !_services.Settings.SearchRightPanel;
        _services.PersistSettings();
        OnPropertyChanged(nameof(IsRightPanelSearched));
        OnPropertyChanged(nameof(IsRightPanelLocked));
        RecomputeBestItems();
    });

    public void ExecuteEquipAllBest() => GameLog.Guard("equip all", () =>
    {
        var character = _gateway.CurrentCharacter;
        if (character is null) return;

        EquipAllFor(new[] { character });
    });

    public void ExecuteEquipAllCharacters() => GameLog.Guard("equip all", () =>
        EquipAllFor(_gateway.GetEquippableHeroes()));

    /// <summary>
    ///     Plans every equip up front and executes them as ONE transfer batch —
    ///     per-command transfers make the game rebuild the trade UI for each
    ///     item, which visibly freezes large inventories. A claims map keeps
    ///     two slots or heroes from planning the same physical item, and items
    ///     displaced by earlier steps join the candidate pool for later ones:
    ///     the sword replaced on the first hero may still be the best option
    ///     for the last one.
    /// </summary>
    private void EquipAllFor(IEnumerable<CharacterObject> characters)
    {
        var commands = new List<TransferCommand>();
        var claimedCounts = new Dictionary<SPItemVM, int>();
        var releasedItems = new MBBindingList<SPItemVM>();

        bool IsExhausted(SPItemVM item) =>
            claimedCounts.TryGetValue(item, out var claimed) && claimed >= item.ItemCount;

        foreach (var character in characters)
        {
            var equipment = _gateway.GetEquipmentFor(character);
            if (equipment is null) continue;

            foreach (var slot in _slots)
            {
                var query = _services.Profiles.GetQuery(character, equipment, slot.Slot);
                var found = _services.EquipBest.FindBest(
                    _gateway, query, slot.Slot, character, equipment, IsExhausted, releasedItems);
                if (found is null) continue;

                claimedCounts.TryGetValue(found, out var claimed);
                claimedCounts[found] = claimed + 1;
                commands.Add(_gateway.BuildEquipCommand(found, slot.Slot, character));

                var displaced = equipment[slot.Slot];
                if (_gateway.CreateReleasedItemVM(displaced) is { } releasedItem)
                    releasedItems.Add(releasedItem);
            }
        }

        _gateway.EquipBatch(commands);

        GameLog.Info(new TextObject("{=EbiEquippedCount}Equipped {COUNT} item(s).")
            .SetTextVariable("COUNT", commands.Count).ToString());
    }

    /// <summary>Called when inventory contents or the shown character change.</summary>
    internal void OnInventoryChanged() => GameLog.Guard("search", () =>
    {
        _services.EquipBest.InvalidateCaches();

        if (SlotSettings.IsVisible)
            SlotSettings.ExecuteClose();

        RecomputeBestItems();
    });

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

    /// <summary>
    ///     Explains the slot's current pick to the message log: why the found
    ///     item beats the equipped one (or why nothing was found), under the
    ///     active search method. Deterministic — same scoring as the search.
    /// </summary>
    private void ExplainSlot(CharacterObject character, Equipment equipment, EquipmentIndex slot)
    {
        var query = _services.Profiles.GetQuery(character, equipment, slot);
        var found = _services.EquipBest.FindBest(_gateway, query, slot, character, equipment);
        var explanation = _services.Explainer.Explain(character, equipment, slot, query, ActiveMode(), found);
        ExplanationFormatter.Log(explanation);
    }

    /// <summary>
    ///     Deterministic recommendation facts for the shown hero's active set,
    ///     one line per slot with an upgrade — fed to the AI so it can narrate
    ///     "why" answers without inventing numbers. Runs on the main thread.
    /// </summary>
    private string CollectSlotExplanations()
    {
        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;
        if (character is null || equipment is null) return "";

        var mode = ActiveMode();
        var lines = new List<string>();
        foreach (var slot in _slots)
        {
            var query = _services.Profiles.GetQuery(character, equipment, slot.Slot);
            var found = _services.EquipBest.FindBest(_gateway, query, slot.Slot, character, equipment);
            if (found is null) continue;

            var explanation = _services.Explainer.Explain(character, equipment, slot.Slot, query, mode, found);
            var fact = ExplanationFormatter.ToPromptFact(explanation);
            if (fact.Length > 0) lines.Add(fact);
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    ///     "Why not [named item]" for a slot (asked through the AI): finds the
    ///     item by name in the searched panels and explains deterministically
    ///     why it wasn't picked. No native-widget hooks needed — the AI only
    ///     supplies the slot and the name.
    /// </summary>
    private void ExplainNamedItem(EquipmentIndex slot, string name)
    {
        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;
        if (character is null || equipment is null) return;

        var candidate = FindCandidateByName(name);
        if (candidate is null)
        {
            ExplanationFormatter.Log(new SearchExplanation
            {
                Slot = slot, Mode = ActiveMode(), Kind = ExplanationKind.NamedNotFound, QueriedName = name
            });
            return;
        }

        var query = _services.Profiles.GetQuery(character, equipment, slot);
        var found = _services.EquipBest.FindBest(_gateway, query, slot, character, equipment);
        var explanation = _services.Explainer.ExplainRejection(
            character, equipment, slot, query, ActiveMode(), candidate, found, name);
        ExplanationFormatter.Log(explanation);
    }

    /// <summary>First item in a searched panel whose name contains the query (case-insensitive).</summary>
    private SPItemVM? FindCandidateByName(string name)
    {
        var needle = name.Trim();
        var left = _services.Settings.SearchLeftPanel ? _gateway.LeftItems : null;
        var right = _services.Settings.SearchRightPanel ? _gateway.RightItems : null;
        return MatchByName(right, needle) ?? MatchByName(left, needle);
    }

    private static SPItemVM? MatchByName(MBBindingList<SPItemVM>? list, string needle)
    {
        if (list is null) return null;

        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            var itemName = item?.ItemRosterElement.EquipmentElement.GetModifiedItemName()?.ToString();
            if (itemName is not null && itemName.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return item;
        }

        return null;
    }

    private SearchMode ActiveMode() =>
        _services.Settings.UsePriority ? SearchMode.Priority
        : _services.Settings.UseEffectiveness ? SearchMode.Effectiveness
        : SearchMode.Weights;

    private void EquipFound(EbiSlotVM slot)
    {
        if (slot.FoundItem is null) return;

        _services.EquipBest.Equip(_gateway, slot.FoundItem, slot.Slot);
        // The transfer fires InventoryLogic.AfterTransfer, which recomputes buttons.
    }

    /// <summary>"#RRGGBB" or "#RRGGBBAA"; anything unparsable falls back to white (no tint).</summary>
    private static Color ParseColor(string hex)
    {
        try
        {
            var value = (hex ?? "").Trim();
            if (value.Length == 7) value += "FF";
            return Color.ConvertStringToColor(value);
        }
        catch
        {
            return Color.White;
        }
    }

    private void OpenSettings(EquipmentIndex slot)
    {
        var character = _gateway.CurrentCharacter;
        var equipment = _gateway.ActiveEquipment;
        if (character is null || equipment is null) return;

        SlotSettings.Open(character, equipment, slot);
    }
}
