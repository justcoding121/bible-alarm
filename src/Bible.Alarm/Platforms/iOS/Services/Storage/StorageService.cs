using System.Reflection;

namespace Bible.Alarm.Platforms.iOS.Services.Storage
{
    public class IOsStorageService : StorageService
    {
        //backed up to cloud
        private static readonly string storageRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "..", "Library");

        public override string StorageRoot => storageRoot;

        //never backed up to cloud
        //system may delete file if needed when app is not running.
        private static readonly string cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "..", "Library", "Caches");

        public override string CacheRoot => cacheRoot;

        public override Assembly MainAssembly => typeof(IOsStorageService).Assembly;
    }
}