using System.Reflection;
using Bible.Alarm.Common.Interfaces.Platform;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Returns the app version from the Bible.Alarm assembly (AssemblyInformationalVersion),
/// which is set from ApplicationDisplayVersion in Bible.Alarm.csproj. Used for version-change
/// checks (media index, schedule DB) and logging so all platforms use the same csproj version.
/// </summary>
public sealed class AssemblyAppVersionFinder : IVersionFinder
{
    private static readonly Lazy<string> Version = new(GetVersionFromAssembly);

    public string GetVersionName() => Version.Value;

    private static string GetVersionFromAssembly()
    {
        var asm = typeof(AssemblyAppVersionFinder).Assembly;
        var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return !string.IsNullOrEmpty(attr?.InformationalVersion) ? attr.InformationalVersion : "0.0.0";
    }
}
