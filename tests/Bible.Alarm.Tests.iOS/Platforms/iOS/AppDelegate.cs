#nullable enable

using System.Linq;
using Foundation;
using UIKit;

namespace Bible.Alarm.Tests.iOS;

/// <summary>
/// AppDelegate for the iOS device-test bundle. We schedule <see cref="TestEntryPoint.RunAsync"/> on the
/// main runloop right after launch — running it inline from <c>FinishedLaunching</c> would deadlock
/// because xunit's test runner blocks on the main thread.
///
/// Registered under a *distinct* Objective-C class name (BibleAlarmTestsAppDelegate) so it does not
/// collide with Bible.Alarm.Platforms.iOS.AppDelegate (registered as "AppDelegate") that ships in the
/// referenced production assembly. <see cref="Program"/> wires UIApplication to this class explicitly.
/// </summary>
[Register("BibleAlarmTestsAppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        // A hidden root window keeps UIKit happy; xharness only cares about exit code + results file.
        var windowScene = application.ConnectedScenes.OfType<UIWindowScene>().FirstOrDefault();
        UIWindow window;
        if (windowScene is not null)
        {
            window = new UIWindow(windowScene);
        }
        else
        {
#pragma warning disable CA1422 // UIScreen-based UIWindow is obsolete on iOS 26+; used only when no scene exists yet.
            window = new UIWindow(UIScreen.MainScreen.Bounds);
#pragma warning restore CA1422
        }

        window.RootViewController = new UIViewController();
        window.MakeKeyAndVisible();
        Window = window;

        // Defer until the runloop is pumping so xunit can post completion to the main thread.
        UIApplication.SharedApplication.BeginInvokeOnMainThread(async () =>
        {
            var entryPoint = new TestEntryPoint();
            try
            {
                await entryPoint.RunAsync().ConfigureAwait(true);
                // xharness reads the simulator's exit code: 0 = success.
                System.Environment.Exit(0);
            }
            catch
            {
                System.Environment.Exit(1);
            }
        });

        return true;
    }
}
