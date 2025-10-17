using Newtonsoft.Json;

namespace Bible.Alarm.Common.Extensions
{
    public static class CloneExtensions
    {
        public static T DeepClone<T>(this T obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
