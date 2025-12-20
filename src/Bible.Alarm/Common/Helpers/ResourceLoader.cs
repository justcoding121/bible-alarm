using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Utility class that can be used to find and load embedded resources into memory.
/// </summary>
/// <remarks>
/// https://github.com/xamarin/mobile-samples/blob/master/EmbeddedResources/SharedLib/ResourceLoader.cs
/// </remarks>
public static class ResourceLoader
{
    /// <summary>
    /// Attempts to find and return the given resource from within the specified assembly.
    /// </summary>
    public static Stream GetEmbeddedResourceStream(Assembly assembly, string resourceFileName)
    {
        var resourceNames = assembly.GetManifestResourceNames();

        var resourcePaths = resourceNames
            .Where(x => x.EndsWith(resourceFileName, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();

        if (resourcePaths.Length == 0)
        {
            throw new Exception($"Resource ending with {resourceFileName} not found.");
        }

        if (resourcePaths.Length > 1)
        {
            throw new Exception(
                $"Multiple resources ending with {resourceFileName} found: {Environment.NewLine}{string.Join(Environment.NewLine, resourcePaths)}");
        }

        var stream = assembly.GetManifestResourceStream(resourcePaths.Single()) ?? throw new Exception(
            $"Resource stream for {resourceFileName} is null.");
        return stream;
    }

    [RequiresDynamicCode("Assembly.Location may not be available in AOT scenarios. Consider using GetManifestResourceStream instead.")]
    [RequiresAssemblyFiles]
    public static FileInfo GetFileInfo(Assembly assembly)
    {
        // Assembly.Location is not available in AOT/trimmed scenarios
        // This method should only be used when AOT is not enabled
        return string.IsNullOrEmpty(assembly.Location) ? throw new InvalidOperationException("Assembly.Location is not available. This method cannot be used in AOT/trimmed scenarios.") : new FileInfo(assembly.Location);
    }
}

