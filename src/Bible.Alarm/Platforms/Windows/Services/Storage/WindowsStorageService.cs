using System;
using System.IO;
using System.Reflection;
using Bible.Alarm.Services.Storage;
using Windows.Storage;

namespace Bible.Alarm.Platforms.Windows.Services.Storage;

public class WindowsStorageService : StorageService, IDisposable
{
    private bool isDisposed;

    private static string GetStorageRoot()
    {
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch (InvalidOperationException)
        {
            return GetFallbackRoot();
        }
    }

    private static string GetCacheRoot()
    {
        try
        {
            return ApplicationData.Current.LocalCacheFolder.Path;
        }
        catch (InvalidOperationException)
        {
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

    public override void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // No resources to dispose
        base.Dispose();
    }
}
