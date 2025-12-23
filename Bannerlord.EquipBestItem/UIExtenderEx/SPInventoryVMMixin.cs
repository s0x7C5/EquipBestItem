using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Bannerlord.EquipBestItem.ViewModels;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.EquipBestItem.UIExtenderEx;

[ViewModelMixin("RefreshValues")]
public class SPInventoryVMMixin : BaseViewModelMixin<SPInventoryVM>
{
    [DataSourceProperty]
    public ModInventoryVM ModInventory { get; }

    public SPInventoryVMMixin(SPInventoryVM vm) : base(vm)
    {
        ModInventory = new ModInventoryVM();
        RegisterEvents();
    }

    public override void OnRefresh()
    {
        base.OnRefresh();
        Update();
    }

    public override void OnFinalize()
    {
        UnregisterEvents();
        base.OnFinalize();
    }
    
    private void Update()
    {
        Helper.ShowMessage($"UpdateValues");
    }
    
    private void RegisterEvents()
    {
        if (ViewModel == null) return;

        ViewModel.PropertyChangedWithBoolValue += SPInventoryVM_PropertyChangedWithBoolValue;
        
        Game.Current.EventManager.RegisterEvent(
            new Action<InventoryEquipmentTypeChangedEvent>(OnInventoryEquipmentTypeChanged));
    }
    
    private void UnregisterEvents()
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChangedWithBoolValue -= SPInventoryVM_PropertyChangedWithBoolValue;
        }

        Game.Current.EventManager.UnregisterEvent(
            new Action<InventoryEquipmentTypeChangedEvent>(OnInventoryEquipmentTypeChanged));
    }

    private void SPInventoryVM_PropertyChangedWithBoolValue(object sender, PropertyChangedWithBoolValueEventArgs e)
    {
        switch (e.PropertyName)
        {
            case "IsRefreshed" when e.Value:
                Update();
                break;
            case "CurrentCharacterName":
                Update();
                break;
        }
    }

    private void OnInventoryEquipmentTypeChanged(InventoryEquipmentTypeChangedEvent obj)
    {
        Update();
    }
}