#nullable enable

using System.Reflection;

namespace Bible.Alarm.Tests.iOS;

/// <summary>
/// iOS twin of <c>FlushCoverletTrackers</c> in
/// <c>tests/Bible.Alarm.Tests.Android/Platforms/Android/TestRunnerActivity.cs</c>.
///
/// coverlet.msbuild registers <see cref="AppDomain.ProcessExit"/> as the flush hook for the
/// per-module <c>Coverlet.Core.Instrumentation.Tracker.*</c> types it injects. On Mono iOS that
/// event is unreliable when invoked through <see cref="System.Environment.Exit(int)"/> from inside
/// <c>UIApplication.SharedApplication.BeginInvokeOnMainThread</c> — the main run loop is torn down
/// before the .NET runtime walks the handler chain, leaving the OpenCover XML unwritten.
///
/// We invoke each tracker's static <c>UnloadModule(object, EventArgs)</c> manually right before
/// <see cref="System.Environment.Exit(int)"/>. Best-effort: any per-module failure is swallowed so
/// a partial result is still produced (each tracker writes its own file).
/// </summary>
internal static class CoverletTrackerFlush
{
    public static void InvokeAll()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type is null
                    || type.FullName is null
                    || !type.FullName.StartsWith("Coverlet.Core.Instrumentation.Tracker.", StringComparison.Ordinal))
                {
                    continue;
                }

                var unload = type.GetMethod(
                    "UnloadModule",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(object), typeof(EventArgs) },
                    modifiers: null);
                if (unload is null)
                {
                    continue;
                }

                try
                {
                    unload.Invoke(null, new object?[] { null, EventArgs.Empty });
                }
                catch
                {
                    // Per-module failure should not block the rest of the flush; coverlet writes
                    // a separate file per module so a partial result is still useful.
                }
            }
        }
    }
}
