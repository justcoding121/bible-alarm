#nullable enable

using Serilog;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Helper to warm up assemblies that are commonly used after bootstrap.
/// On Android with FastDev (debug mode), assembly loading is very slow (500ms-2s per assembly).
/// Pre-loading these assemblies during bootstrap prevents UI freezes when they're first accessed.
/// </summary>
public static class AssemblyWarmupHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(AssemblyWarmupHelper));

    /// <summary>
    /// Warms up HTTP/download-related assemblies by triggering their static initializers.
    /// This should be called during bootstrap on a background thread.
    /// </summary>
    public static async Task WarmupHttpAssembliesAsync()
    {
#if DEBUG
        var startTime = DateTime.UtcNow;
        logger.Information("[BOOTSTRAP] Starting HTTP assembly warmup");
#endif

        await Task.Run(() =>
        {
            try
            {
                // Trigger loading of System.Net.Http and related assemblies
                // by accessing types that cause assembly loading
                WarmupType<System.Net.Http.HttpClient>();
                WarmupType<System.Net.Http.HttpRequestMessage>();
                WarmupType<System.Net.Http.HttpResponseMessage>();

                // System.IO.Pipelines (used by HttpClient)
                WarmupType<System.IO.Pipelines.Pipe>();

                // System.IO.Compression (used for HTTP compression)
                WarmupType<System.IO.Compression.GZipStream>();
                WarmupType<System.IO.Compression.DeflateStream>();

                // System.Text.Json (commonly used for API responses)
                WarmupType<System.Text.Json.JsonDocument>();

                // TagLibSharp (used for metadata extraction)
                // This one is particularly slow to load
                WarmupType<TagLib.File>();

#if DEBUG
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                logger.Information("[BOOTSTRAP] HTTP assembly warmup completed in {ElapsedMs}ms", elapsed);
#endif
            }
            catch (Exception ex)
            {
                // Non-fatal - assemblies will be loaded on first use
                logger.Debug(ex, "Assembly warmup encountered an error (non-fatal)");
            }
        });
    }

    /// <summary>
    /// Triggers loading of an assembly by accessing a type from it.
    /// Uses RuntimeHelpers.RunClassConstructor to ensure static initialization runs.
    /// </summary>
    private static void WarmupType<T>()
    {
        try
        {
            // Accessing the type handle forces the assembly to load
            // and triggers any static constructors
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
        }
        catch
        {
            // Ignore errors - some types may not have parameterless constructors
            // The goal is just to load the assembly
        }
    }
}
