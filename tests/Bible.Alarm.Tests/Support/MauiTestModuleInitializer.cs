#nullable enable

using System.Runtime.CompilerServices;

namespace Bible.Alarm.Tests.Support;

internal static class MauiTestModuleInitializer
{
    [ModuleInitializer]
    internal static void Run()
    {
        MauiUiTestBootstrap.TryInitialize();
    }
}
