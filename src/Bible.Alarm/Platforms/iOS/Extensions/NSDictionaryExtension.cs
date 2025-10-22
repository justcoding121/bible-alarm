using Foundation;

namespace Bible.Alarm.Platforms.iOS.Extensions
{
    public static class NsDictionaryExtension
    {
        public static NSDictionary ToNsDictionary(this Dictionary<string, string> input)
        {
            return NSDictionary.FromObjectsAndKeys(input.Values.ToArray()
                , input.Keys.ToArray());
        }

        public static Dictionary<string, string> ToDictionary(this NSDictionary input)
        {
            return input.ToDictionary(x => x.Key.ToString(), x => x.Value.ToString());
        }
    }
}