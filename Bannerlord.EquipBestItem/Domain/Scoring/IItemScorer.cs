using TaleWorlds.Core;

namespace Bannerlord.EquipBestItem.Domain.Scoring;

public interface IItemScorer
{
    float Score(EquipmentElement element, in SearchContext context);
}
