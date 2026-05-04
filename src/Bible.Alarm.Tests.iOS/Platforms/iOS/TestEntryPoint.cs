#nullable enable

using Microsoft.DotNet.XHarness.TestRunners.Common;
using Microsoft.DotNet.XHarness.TestRunners.Xunit;

namespace Bible.Alarm.Tests.iOS;

/// <summary>
/// xunit-on-iOS entry point. xharness boots the .app, AppDelegate calls <c>RunAsync</c>, the runner
/// reflects over the assemblies returned by <see cref="GetTestAssemblies"/>, and writes JUnit XML to the
/// path xharness picks up via the simulator's <c>NSDocumentDirectory</c>.
/// </summary>
internal sealed class TestEntryPoint : iOSApplicationEntryPoint
{
    protected override int? MaxParallelThreads => 1;

    protected override IDevice? Device => null;

    protected override bool LogExcludedTests => true;

    protected override IEnumerable<TestAssemblyInfo> GetTestAssemblies()
    {
        // typeof(...).Assembly forces the linker to keep the test assembly in the .app bundle.
        var assembly = typeof(global::Bible.Alarm.Tests.AppSettingsTests).Assembly;
        yield return new TestAssemblyInfo(assembly, assembly.Location);
    }

    protected override void TerminateWithSuccess()
    {
        // Intentional no-op: AppDelegate calls Environment.Exit(0) once RunAsync returns. The base class
        // would call exit() via UIKit which is rejected by App Review tooling — we don't ship this app,
        // but keeping the explicit override makes the intent obvious.
    }
}
