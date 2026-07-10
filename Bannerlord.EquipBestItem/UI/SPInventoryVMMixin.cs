using System;
using System.Collections.Generic;
using Bannerlord.EquipBestItem.Inventory;
using Bannerlord.EquipBestItem.UI.ViewModels;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem.UI;

/// <summary>
///     Attaches the mod's root view model to the native inventory view model
///     and forwards the events that invalidate search state. Thin by design:
///     all behavior lives in <see cref="EbiVM" /> and below.
///
///     Change detection is layered because no single game event covers
///     everything: IsRefreshed misses drag-and-drop and trade-arrow transfers
///     (and our own transfer commands), so InventoryLogic.AfterTransfer /
///     AfterReset are the primary signals, with IsRefreshed kept for the
///     transferless paths (initialization, filters).
/// </summary>
[ViewModelMixin("RefreshValues")]
public sealed class SPInventoryVMMixin : BaseViewModelMixin<SPInventoryVM>
{
    private readonly InventoryGateway _gateway;
    private InventoryLogic? _subscribedLogic;

    [DataSourceProperty]
    public EbiVM ModInventory { get; }

    public SPInventoryVMMixin(SPInventoryVM vm) : base(vm)
    {
        _gateway = new InventoryGateway(vm);
        ModInventory = new EbiVM(ModRuntime.Services, _gateway);

        vm.PropertyChangedWithBoolValue += OnPropertyChangedWithBoolValue;
        vm.PropertyChangedWithValue += OnPropertyChangedWithValue;
        Game.Current.EventManager.RegisterEvent(
            new Action<InventoryEquipmentTypeChangedEvent>(OnEquipmentTypeChanged));

        TrySubscribeInventoryLogic();
    }

    public override void OnRefresh()
    {
        base.OnRefresh();
        // The inventory state may not have been active yet during construction.
        TrySubscribeInventoryLogic();
        ModInventory.OnInventoryChanged();
    }

    public override void OnFinalize()
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChangedWithBoolValue -= OnPropertyChangedWithBoolValue;
            ViewModel.PropertyChangedWithValue -= OnPropertyChangedWithValue;
        }

        if (_subscribedLogic is not null)
        {
            _subscribedLogic.AfterTransfer -= OnAfterTransfer;
            _subscribedLogic.AfterReset -= OnAfterReset;
            _subscribedLogic = null;
        }

        Game.Current.EventManager.UnregisterEvent(
            new Action<InventoryEquipmentTypeChangedEvent>(OnEquipmentTypeChanged));

        ModInventory.OnFinalize();
        base.OnFinalize();
    }

    private void TrySubscribeInventoryLogic()
    {
        if (_subscribedLogic is not null) return;

        _subscribedLogic = _gateway.Logic;
        if (_subscribedLogic is null) return;

        _subscribedLogic.AfterTransfer += OnAfterTransfer;
        _subscribedLogic.AfterReset += OnAfterReset;
    }

    private void OnAfterTransfer(InventoryLogic logic, List<TransferCommandResult> results)
    {
        ModInventory.OnInventoryChanged();
    }

    private void OnAfterReset(InventoryLogic logic, bool fromCancel)
    {
        ModInventory.OnInventoryChanged();
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
