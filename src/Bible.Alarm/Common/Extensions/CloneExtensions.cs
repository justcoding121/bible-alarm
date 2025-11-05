using Newtonsoft.Json;

namespace Bible.Alarm.Common.Extensions;

public static class CloneExtensions
{
    public static T DeepClone<T>(this T obj)
    {
        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None
        };
        var json = JsonConvert.SerializeObject(obj, settings);
        return JsonConvert.DeserializeObject<T>(json, settings);
    }
}