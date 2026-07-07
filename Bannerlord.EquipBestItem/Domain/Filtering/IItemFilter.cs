using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace Bannerlord.EquipBestItem.Domain.Filtering;

/// <summary>
///     One composable acceptance rule. The finder runs all filters over each
///     candidate; a filter that does not apply to the current slot returns true.
/// </summary>
public interface IItemFilter
{
    bool IsSatisfiedBy(SPItemVM item, in SearchContext context);
}
