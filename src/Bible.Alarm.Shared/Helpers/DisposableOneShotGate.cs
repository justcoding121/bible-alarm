#nullable enable

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Ensures disposable teardown runs once while concurrent callers observe an immediate no-op after the first transition.
/// </summary>
public static class DisposableOneShotGate
{
    public static bool TryBegin(ref bool disposedFlag)
    {
        if (disposedFlag)
        {
            return false;
        }

        disposedFlag = true;
        return true;
    }
}
