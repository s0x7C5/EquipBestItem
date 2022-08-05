using System.Collections.Generic;
using System.Linq;
using EquipBestItem.Models.BestItemSearcher;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Dropdown;
using MCM.Abstractions.Settings.Base.Global;

namespace EquipBestItem.Models;

internal class MCMSettings : AttributeGlobalSettings<MCMSettings>
{
    public override string Id => "EquipBestItemSettings";
    public override string DisplayName => "Equip Best Item";
    public override string FolderName => "EquipBestItem";
    public override string FormatType => "json";

    private static readonly IEnumerable<SearcherBase> Searchers = 
        Helper.GetEnumerableOfType<SearcherBase>(new object[] { null! });
    
    [SettingPropertyDropdown(ModTexts.SearcherMethodOption, RequireRestart = false)]
    public DropdownDefault<string> SearchMethod { get; set; } = new(Searchers.Select(c => c.Name), selectedIndex: 0);

    [SettingPropertyDropdown(ModTexts.CultureOption, RequireRestart = false)]
    public bool IsCultureEnabled { get; set; } = true;
    
    [SettingPropertyDropdown(ModTexts.UnequipOption, RequireRestart = false)]
    public bool IsUnequipFeatureEnabled { get; set; } = true;
}