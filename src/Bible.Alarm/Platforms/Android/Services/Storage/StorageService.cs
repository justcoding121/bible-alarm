using Bible.Alarm.Services;
using System.Reflection;

namespace Bible.Alarm.Droid.Services.Storage;

public class AndroidStorageService : StorageService
{
    public override string StorageRoot =>
        //never backed up to cloud
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public override string CacheRoot =>
        //never backed up to cloud
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);


    public override Assembly MainAssembly => typeof(AndroidStorageService).Assembly;
}