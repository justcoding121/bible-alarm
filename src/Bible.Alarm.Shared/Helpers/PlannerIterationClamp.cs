#nullable enable

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Keeps notification occurrence iteration budgets within explicit inclusive bounds.
/// </summary>
public static class PlannerIterationClamp
{
    public static int Normalize(int configured, int minimumInclusive, int maximumInclusive)
    {
        if (configured < minimumInclusive)
        {
            return minimumInclusive;
        }

        if (configured > maximumInclusive)
        {
            return maximumInclusive;
        }

        return configured;
    }
}
