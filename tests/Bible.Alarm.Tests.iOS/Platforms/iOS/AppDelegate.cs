#nullable enable

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

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // A hidden root window keeps UIKit happy; xharness only cares about exit code + results file.
        Window = new UIWindow(UIScreen.MainScreen.Bounds)
        {
            RootViewController = new UIViewController(),
        };
        Window.MakeKeyAndVisible();

        // coverlet.msbuild bakes CoverletOutput into the recorder at build time as a literal string.
        // We pass a *relative* path (`coverage-ios.xml`) from .github/workflows/build.yml so the
        // recorder's File.Open call resolves against the current working directory at runtime.
        // The simulator-app default CWD is the read-only bundle parent, so we relocate to
        // NSDocumentDirectory here — that path lives at
        //   ~/Library/Developer/CoreSimulator/Devices/<UUID>/data/Containers/Data/Application/<UUID>/Documents/
        // on the macOS host, which the workflow's "Pull iOS coverage from simulator container"
        // step then `find`s and copies into artifacts/coverage-ios/coverage-ios.xml.
        try
        {
            var docs = Foundation.NSSearchPath.GetDirectories(
                Foundation.NSSearchPathDirectory.DocumentDirectory,
                Foundation.NSSearchPathDomain.User);
            if (docs is { Length: > 0 } && !string.IsNullOrEmpty(docs[0]))
            {
                System.Environment.CurrentDirectory = docs[0];
            }
        }
        catch
        {
            // Failure to relocate just means coverage-ios.xml lands wherever Mono's default CWD is;
            // the workflow's host-side `find` step still searches the whole simulator container
            // tree, so this is best-effort, not load-bearing.
        }

        // Defer until the runloop is pumping so xunit can post completion to the main thread.
        UIApplication.SharedApplication.BeginInvokeOnMainThread(async () =>
        {
            var entryPoint = new TestEntryPoint();
            int exitCode;
            try
            {
                await entryPoint.RunAsync().ConfigureAwait(true);
                exitCode = 0;
            }
            catch
            {
                exitCode = 1;
            }
            finally
            {
                // coverlet's AppDomain.ProcessExit hook is unreliable on Mono iOS during
                // System.Environment.Exit (the main run loop tears down before .NET walks the
                // handler chain), so we invoke each per-module tracker's UnloadModule manually
                // here. Mirrors FlushCoverletTrackers in the Android TestRunnerActivity.
                try { CoverletTrackerFlush.InvokeAll(); } catch { /* best-effort */ }
            }

            // xharness reads the simulator's exit code: 0 = success.
            System.Environment.Exit(exitCode);
        });

        return true;
    }
}
