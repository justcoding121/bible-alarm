using System.Reflection;
using Bible.Alarm.Services.Storage;
using Windows.Storage;

namespace Bible.Alarm.Platforms.Windows.Services.Storage;

public class WindowsStorageService : StorageService, IDisposable
{
    private bool isDisposed;

    private static string GetStorageRoot() => ApplicationData.Current.LocalFolder.Path;
    private static string GetCacheRoot() => ApplicationData.Current.LocalCacheFolder.Path;

    private static readonly string storageRoot = GetStorageRoot();
    public override string StorageRoot => storageRoot;

    private static readonly string cacheRoot = GetCacheRoot();
    public override string CacheRoot => cacheRoot;
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
