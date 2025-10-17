using Bible.Alarm.Services;
using System;
using System.IO;
using System.Reflection;
using Windows.Storage;

namespace Bible.Alarm.Uwp.Services.Storage
{
    public class UwpStorageService : StorageService
    {
        //backed up to cloud
        private static string storageRoot = ApplicationData.Current.LocalFolder.Path;
        public override string StorageRoot
        {
            get
            {
                return storageRoot;
            }
        }

        //never backed up to cloud.
        //never deleted by system.
        private static string cacheRoot = ApplicationData.Current.LocalCacheFolder.Path;
        public override string CacheRoot => cacheRoot;
        public override Assembly MainAssembly => typeof(UwpStorageService).Assembly;
    }
}