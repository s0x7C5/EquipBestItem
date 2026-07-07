using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace Bannerlord.EquipBestItem.Domain.Filtering;

/// <summary>Applies the optional hard constraints carried by the query.</summary>
public sealed class QueryConstraintFilter : IItemFilter
{
    public bool IsSatisfiedBy(SPItemVM item, in SearchContext context)
    {
        var query = context.Query;
        var element = item.ItemRosterElement.EquipmentElement;

        if (query.MaxItemWeight > 0f && element.Item?.Weight > query.MaxItemWeight) return false;

        if (query.CultureId is { } cultureId &&
            !string.Equals(element.Item?.Culture?.StringId, cultureId, System.StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
