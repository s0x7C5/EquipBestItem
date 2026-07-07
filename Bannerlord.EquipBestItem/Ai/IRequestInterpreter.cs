using System.Threading;
using System.Threading.Tasks;

namespace Bannerlord.EquipBestItem.Ai;

/// <summary>
///     Turns a free-form player request ("одень меня в самую лёгкую броню
///     империи") into structured equip directives. Implementations must be
///     safe to call from a background thread.
/// </summary>
public interface IRequestInterpreter
{
    Task<InterpretedPlan> InterpretAsync(string request, InterpretationContext context, CancellationToken cancellationToken);
}
