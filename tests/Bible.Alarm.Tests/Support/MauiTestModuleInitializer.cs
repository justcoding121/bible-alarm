#nullable enable

using System.Runtime.CompilerServices;

namespace Bible.Alarm.Tests.Support;

internal static class MauiTestModuleInitializer
{
    // Do not bootstrap MAUI here. Eager CreateMauiApp/UseMauiApp in a ModuleInitializer
    // runs before the vstest host is ready and can poison Microsoft.Maui.Controls.Element
    // static initialization (FocusManager COM) for the entire test process.
    [ModuleInitializer]
    internal static void Run()
    {
    }
}
