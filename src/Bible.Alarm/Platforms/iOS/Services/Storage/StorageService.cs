using Bible.Alarm.Services;
using System;
using System.IO;
using System.Reflection;

namespace Bible.Alarm.Droid.Services.Storage
{
    public class iOSStorageService : StorageService
    {
        //backed up to cloud
        private static string storageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "..", "Library");
        public override string StorageRoot
        {
            get
            {
                return storageRoot;
            }
        }

        //never backed up to cloud
        //system may delete file if needed when app is not running.
        private static string cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "..", "Library", "Caches");
        public override string CacheRoot => cacheRoot;

        public override Assembly MainAssembly => typeof(iOSStorageService).Assembly;
    }
}