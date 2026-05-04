#nullable enable

using UIKit;

namespace Bible.Alarm.Tests.iOS;

/// <summary>
/// iOS process entry point. Hands control to our test <see cref="AppDelegate"/> (registered as
/// BibleAlarmTestsAppDelegate to avoid colliding with Bible.Alarm.Platforms.iOS.AppDelegate's
/// "AppDelegate" registration). The test AppDelegate then drives the xharness xunit runner under
/// <see cref="TestEntryPoint"/>.
///
/// Bible.Alarm's Program.Main also exists in the referenced assembly, but only the entry assembly's
/// Main (this one) is the executable entry point — Bible.Alarm.Program.Main is dead code in this APK/.app.
/// </summary>
public static class Program
{
    private static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
