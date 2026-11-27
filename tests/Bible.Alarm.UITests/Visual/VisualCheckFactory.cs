#nullable enable

namespace Bible.Alarm.UITests.Visual;

public static class VisualCheckFactory
{
    public static IVisualCheck Create(string platform)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("VISUAL_CHECKS"), "off", StringComparison.OrdinalIgnoreCase))
        {
            return new NullVisualCheck();
        }

        var provider = Environment.GetEnvironmentVariable("VISUAL_PROVIDER");
        if (string.IsNullOrWhiteSpace(provider) || provider.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            return new LocalBaselineVisualCheck(platform);
        }

        Console.WriteLine($"Visual provider '{provider}' is not built in yet. Using the local baseline diff.");
        return new LocalBaselineVisualCheck(platform);
    }
}
