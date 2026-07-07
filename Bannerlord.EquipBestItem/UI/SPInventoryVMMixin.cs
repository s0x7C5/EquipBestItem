using System;
using Bannerlord.EquipBestItem.Inventory;
using Bannerlord.EquipBestItem.UI.ViewModels;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem.UI;

/// <summary>
///     Attaches the mod's root view model to the native inventory view model
///     and forwards the events that invalidate search state. Thin by design:
///     all behavior lives in <see cref="EbiVM" /> and below.
/// </summary>
[ViewModelMixin("RefreshValues")]
public sealed class SPInventoryVMMixin : BaseViewModelMixin<SPInventoryVM>
{
    [DataSourceProperty]
    public EbiVM ModInventory { get; }

    public SPInventoryVMMixin(SPInventoryVM vm) : base(vm)
    {
        ModInventory = new EbiVM(ModRuntime.Services, new InventoryGateway(vm));

        vm.PropertyChangedWithBoolValue += OnPropertyChangedWithBoolValue;
        vm.PropertyChangedWithValue += OnPropertyChangedWithValue;
        Game.Current.EventManager.RegisterEvent(
            new Action<InventoryEquipmentTypeChangedEvent>(OnEquipmentTypeChanged));
    }

    public override void OnFinalize()
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChangedWithBoolValue -= OnPropertyChangedWithBoolValue;
            ViewModel.PropertyChangedWithValue -= OnPropertyChangedWithValue;
        }

        Game.Current.EventManager.UnregisterEvent(
            new Action<InventoryEquipmentTypeChangedEvent>(OnEquipmentTypeChanged));

        ModInventory.OnFinalize();
        base.OnFinalize();
    }

    private void OnPropertyChangedWithBoolValue(object? sender, PropertyChangedWithBoolValueEventArgs e)
    {
        if (e.PropertyName == "IsRefreshed" && e.Value)
            ModInventory.OnInventoryChanged();
    }

    private void OnPropertyChangedWithValue(object? sender, PropertyChangedWithValueEventArgs e)
    {
        if (e.PropertyName == "CurrentCharacterName")
            ModInventory.OnInventoryChanged();
    }

    private void OnEquipmentTypeChanged(InventoryEquipmentTypeChangedEvent _)
    {
        ModInventory.OnInventoryChanged();
    }
}
