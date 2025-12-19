using System.Reflection;
using Bible.Alarm.Services.Storage;

namespace Bible.Alarm.Platforms.iOS.Services.Storage
{
    public class iOSStorageService : StorageService, IDisposable
    {
        private bool _isDisposed;
        //backed up to cloud
        private static readonly string storageRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "..", "Library");

        public override string StorageRoot => storageRoot;

        //never backed up to cloud
        //system may delete file if needed when app is not running.
        private static readonly string cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "..", "Library", "Caches");

        public override string CacheRoot => cacheRoot;

        public override Assembly MainAssembly => typeof(iOSStorageService).Assembly;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            // No resources to dispose
        }
    }
}