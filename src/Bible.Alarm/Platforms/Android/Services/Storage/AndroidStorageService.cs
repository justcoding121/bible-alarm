using System.Reflection;
using Bible.Alarm.Services.Storage;

namespace Bible.Alarm.Platforms.Android.Services.Storage;

public class AndroidStorageService : StorageService, IDisposable
{
    private bool isDisposed;
    public override string StorageRoot =>
        //never backed up to cloud
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public override string CacheRoot =>
        //never backed up to cloud
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);


    public override Assembly MainAssembly => typeof(AndroidStorageService).Assembly;

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
