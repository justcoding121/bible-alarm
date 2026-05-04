#nullable enable

using Android.App;
using Android.OS;
using Android.Runtime;
using Microsoft.DotNet.XHarness.DefaultAndroidEntryPoint.Xunit;

namespace Bible.Alarm.Tests.Android;

/// <summary>
/// xharness invokes this instrumentation via `am instrument -w com.jthomas.info.Bible.Alarm.Tests/.TestInstrumentation`
/// and passes its CLI knobs (results-file-path, include-class, exclude-class, ...) as bundle extras.
/// We forward those into <see cref="DefaultAndroidEntryPoint"/>, list the assemblies that contain xunit
/// fixtures (so the linker keeps them in the APK), and let it run synchronously. The exit code returned via
/// <c>Finish()</c> is what xharness uses to decide pass/fail.
/// </summary>
[Instrumentation(Name = "com.jthomas.info.Bible.Alarm.Tests.TestInstrumentation")]
public sealed class TestInstrumentation : Instrumentation
{
    private const string DefaultResultsDir = "/sdcard/Documents/test-results";

    private Bundle? _arguments;

    // Public ctor is required: the Android runtime constructs the Instrumentation via JNI when
    // `am instrument` runs, and the binding helper calls this overload by reflection.
    public TestInstrumentation(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        _arguments = arguments;
        Start();
    }

    public override async void OnStart()
    {
        base.OnStart();

        var bundleArgs = BundleToDictionary(_arguments);

        // xharness sets results-file-path to the device path it will pull from after the run. Fall back to
        // a writable default for manual `adb shell am instrument` runs from a developer's box.
        var resultsPath = bundleArgs.GetValueOrDefault(DefaultAndroidEntryPoint.ResultsFileArgumentPath)
            ?? DefaultResultsDir;

        var entryPoint = new BibleAlarmAndroidEntryPoint(resultsPath, bundleArgs)
        {
            // Loading the test assemblies by typeof(...) guarantees the AOT/linker keeps them in the APK.
            Tests = new[]
            {
                typeof(global::Bible.Alarm.Tests.AppSettingsTests).Assembly,
            },
        };

        Bundle resultBundle = new();
        try
        {
            await entryPoint.RunAsync().ConfigureAwait(false);
            resultBundle.PutInt("return-code", 0);
            Finish(Result.Ok, resultBundle);
        }
        catch (Exception ex)
        {
            resultBundle.PutString("error", ex.ToString());
            resultBundle.PutInt("return-code", 1);
            Finish(Result.Canceled, resultBundle);
        }
    }

    private static Dictionary<string, string> BundleToDictionary(Bundle? bundle)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (bundle is null)
        {
            return dict;
        }

        foreach (var key in bundle.KeySet() ?? Array.Empty<string>())
        {
            var value = bundle.GetString(key);
            if (value is not null)
            {
                dict[key] = value;
            }
        }

        return dict;
    }

    /// <summary>
    /// Subclass that pins MaxParallelThreads to 1. Unit tests in this repo touch the singleton SQLite
    /// schedule DB and the static MauiAppHolder; running them in parallel on a single emulator triggers
    /// flaky cross-test contention that does not reproduce on the Windows runner.
    /// </summary>
    private sealed class BibleAlarmAndroidEntryPoint : DefaultAndroidEntryPoint
    {
        public BibleAlarmAndroidEntryPoint(string resultsPath, Dictionary<string, string> arguments)
            : base(resultsPath, arguments)
        {
        }

        protected override int? MaxParallelThreads => 1;
    }
}
