using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.EquipBestItem.UI.ViewModels;

/// <summary>
///     Per-slot button state: the best item found for the slot (null hides the
///     button and its tooltip). Left click equips exactly the previewed item,
///     right click opens the slot's search settings.
/// </summary>
public sealed class EbiSlotVM : ViewModel
{
    private readonly Action<EbiSlotVM> _equip;
    private readonly Action<EquipmentIndex> _openSettings;

    private SPItemVM? _bestItem;
    private bool _isButtonDisabled = true;

    internal EbiSlotVM(EquipmentIndex slot, Action<EbiSlotVM> equip, Action<EquipmentIndex> openSettings)
    {
        Slot = slot;
        _equip = equip;
        _openSettings = openSettings;
        Hint = new HintViewModel(new TextObject(
            "{=EbiSlotButtonHint}Equip the best item. Right click opens the search settings."));
    }

    internal EquipmentIndex Slot { get; }

    [DataSourceProperty]
    public HintViewModel Hint { get; }

    [DataSourceProperty]
    public SPItemVM? BestItem
    {
        get => _bestItem;
        private set
        {
            if (ReferenceEquals(value, _bestItem)) return;
            _bestItem = value;
            OnPropertyChangedWithValue(value);
        }
    }

    [DataSourceProperty]
    public bool IsButtonDisabled
    {
        get => _isButtonDisabled;
        private set
        {
            if (value == _isButtonDisabled) return;
            _isButtonDisabled = value;
            OnPropertyChangedWithValue(value);
        }
    }

    internal void SetBest(SPItemVM? item)
    {
        BestItem = item;
        IsButtonDisabled = item is null;
    }

    public void ExecuteEquip() => _equip(this);

    public void ExecuteOpenSettings() => _openSettings(Slot);
}
