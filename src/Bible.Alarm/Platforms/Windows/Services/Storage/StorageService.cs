using Bible.Alarm.Services.Storage;
using System.Reflection;

namespace Bible.Alarm.Platforms.Windows.Services.Storage
{
    public class UwpStorageService : StorageService
    {
        // Use standard .NET paths instead of UWP ApplicationData
        private static readonly string storageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bible.Alarm", "Data");

        public override string StorageRoot => storageRoot;

        // Use standard .NET cache folder
        private static readonly string cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bible.Alarm", "Cache");
        public override string CacheRoot => cacheRoot;
        public override Assembly MainAssembly => typeof(UwpStorageService).Assembly;
    }
}