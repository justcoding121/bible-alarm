#nullable enable

namespace Bible.Alarm.UITests.Infrastructure;

public static class Waits
{
    public static T Until<T>(Func<T?> probe, TimeSpan timeout, string description) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var value = probe();
                if (value != null)
                {
                    return value;
                }
            }
            catch (Exception ex)
            {
                last = ex;
            }

            Thread.Sleep(500);
        }

        throw new TimeoutException($"Timed out waiting for {description}.", last);
    }

    public static void Until(Func<bool> probe, TimeSpan timeout, string description)
    {
        Until<string>(() => probe() ? "ok" : null, timeout, description);
    }
}
