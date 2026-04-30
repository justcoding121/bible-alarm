using System;
using System.IO;
using System.Reflection;
using Bible.Alarm.Services.Storage;
using Serilog;
using Windows.Storage;

namespace Bible.Alarm.Platforms.Windows.Services.Storage;

public class WindowsStorageService : StorageService
{
    private static string GetStorageRoot()
    {
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch (InvalidOperationException ex)
        {
            Log.Logger.Debug(ex, "WindowsStorageService: ApplicationData.Current.LocalFolder not available, using fallback root");
            return GetFallbackRoot();
        }
    }

    private static string GetCacheRoot()
    {
        try
        {
            return ApplicationData.Current.LocalCacheFolder.Path;
        }
        catch (InvalidOperationException ex)
        {
            Log.Logger.Debug(ex, "WindowsStorageService: ApplicationData.Current.LocalCacheFolder not available, using fallback cache");
            return Path.Combine(GetFallbackRoot(), "Cache");
        }
    }

    private static string GetFallbackRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BibleAlarm");

    private static readonly Lazy<string> StorageRootLazy = new(GetStorageRoot);
    public override string StorageRoot => StorageRootLazy.Value;

    private static readonly Lazy<string> CacheRootLazy = new(GetCacheRoot);
    public override string CacheRoot => CacheRootLazy.Value;
    public override Assembly MainAssembly => typeof(WindowsStorageService).Assembly;
}
