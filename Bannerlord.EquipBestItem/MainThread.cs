using System;
using System.Collections.Concurrent;

namespace Bannerlord.EquipBestItem;

/// <summary>
///     Marshals work from background threads onto the game's main thread.
///     Posted actions run on the next frame, drained from
///     <see cref="SubModule.OnApplicationTick" />.
/// </summary>
internal static class MainThread
{
    private static readonly ConcurrentQueue<Action> Queue = new();

    internal static void Post(Action action)
    {
        Queue.Enqueue(action);
    }

    internal static void Drain()
    {
        while (Queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                GameLog.Error(exception.Message);
            }
        }
    }
}
