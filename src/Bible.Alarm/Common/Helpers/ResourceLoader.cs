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

        if (!resourcePaths.Any())
            throw new Exception(string.Format("Resource ending with {0} not found.", resourceFileName));

        if (resourcePaths.Count() > 1)
            throw new Exception(string.Format("Multiple resources ending with {0} found: {1}{2}", resourceFileName,
                Environment.NewLine, string.Join(Environment.NewLine, resourcePaths)));

        var stream = assembly.GetManifestResourceStream(resourcePaths.Single());
        if (stream == null)
            throw new Exception(string.Format("Resource stream for {0} is null.", resourceFileName));
        
        return stream;
    }

    public static FileInfo GetFileInfo(Assembly assembly)
    {
#pragma warning disable IL3000
        return new FileInfo(assembly.Location);
#pragma warning restore IL3000
    }
}

