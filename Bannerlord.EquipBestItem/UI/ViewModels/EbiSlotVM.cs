using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem.UI.ViewModels;

/// <summary>
///     Per-slot button state. The button is visible only when a better item
///     was found; left click equips exactly the previewed item, right click
///     opens the slot's search settings. The tooltip shows the found item, or
///     the currently equipped one when the button is revealed with Alt.
/// </summary>
public sealed class EbiSlotVM : ViewModel
{
    private readonly Action<EbiSlotVM> _equip;
    private readonly Action<EquipmentIndex> _openSettings;

    private SPItemVM? _foundItem;
    private SPItemVM? _bestItem;
    private bool _isButtonDisabled = true;

    internal EbiSlotVM(EquipmentIndex slot, Action<EbiSlotVM> equip, Action<EquipmentIndex> openSettings)
    {
        Slot = slot;
        _equip = equip;
        _openSettings = openSettings;
    }

    internal EquipmentIndex Slot { get; }

    internal SPItemVM? FoundItem => _foundItem;

    /// <summary>Feeds the native comparison tooltip on hover.</summary>
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

    internal void SetBest(SPItemVM? found, SPItemVM? equipped)
    {
        _foundItem = found;
        BestItem = found ?? equipped;
        IsButtonDisabled = found is null;
    }

    public void ExecuteEquip()
    {
        if (_foundItem is not null)
            _equip(this);
    }

    public void ExecuteOpenSettings() => _openSettings(Slot);
}
