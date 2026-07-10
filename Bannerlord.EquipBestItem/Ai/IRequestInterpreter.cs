using System.Threading;
using System.Threading.Tasks;

namespace Bannerlord.EquipBestItem.Ai;

/// <summary>
///     Turns a free-form player request ("dress me in the lightest imperial
///     armor") into structured equip directives. Implementations must be
///     safe to call from a background thread.
/// </summary>
public interface IRequestInterpreter
{
    Task<InterpretedPlan> InterpretAsync(string request, InterpretationContext context, CancellationToken cancellationToken);
}
