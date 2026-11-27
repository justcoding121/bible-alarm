#nullable enable

namespace Bible.Alarm.Services.Media.BrowseHelpers;

/// <summary>
/// Completes an async browse future on every path. A future that never completes is an endless spinner in the car.
/// </summary>
public static class FutureCompletion
{
    public const int BrowseBootstrapWaitMs = 10_000;

    public static bool TryComplete(Func<bool> setResult, Action<Exception?> onFailure)
    {
        try
        {
            if (setResult())
            {
                return true;
            }

            onFailure(null);
            return false;
        }
        catch (Exception ex)
        {
            onFailure(ex);
            return false;
        }
    }
}
