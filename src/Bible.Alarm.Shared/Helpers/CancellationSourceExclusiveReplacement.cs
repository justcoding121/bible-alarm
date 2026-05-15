#nullable enable

using System;
using System.Threading;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Swaps out and tears down cancellation sources used for exclusive ownership (e.g. a single active toast token).
/// </summary>
public static class CancellationSourceExclusiveReplacement
{
    public static bool TryTakeExclusive(ref CancellationTokenSource? slot, out CancellationTokenSource? taken)
    {
        taken = slot;
        if (taken == null)
        {
            return false;
        }

        slot = null;
        return true;
    }

    public static void CancelDisposeSwallowDisposed(CancellationTokenSource cts)
    {
        try
        {
            cts.Cancel();
            cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Safe to ignore: another thread may have disposed the CTS after Cancel; teardown is complete.
        }
    }
}
