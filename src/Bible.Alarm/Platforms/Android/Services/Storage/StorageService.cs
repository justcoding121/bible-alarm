using Bible.Alarm.Services;
using System;
using System.Reflection;

namespace Bible.Alarm.Droid.Services.Storage
{
    public class AndroidStorageService : StorageService
    {
        public override string StorageRoot
        {
            get
            {
                //never backed up to cloud
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
        }

        public override string CacheRoot
        {
            get
            {
                //never backed up to cloud
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
        }


        public override Assembly MainAssembly => typeof(AndroidStorageService).Assembly;
    }
}