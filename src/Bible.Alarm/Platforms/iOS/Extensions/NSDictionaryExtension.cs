using Foundation;

namespace Bible.Alarm.Platforms.iOS.Extensions;

public static class NsDictionaryExtension
{
    public static NSDictionary ToNsDictionary(this Dictionary<string, string> input)
    {
        return NSDictionary.FromObjectsAndKeys(input.Values.ToArray<object>()
            , input.Keys.ToArray<object>());
    }

    public static Dictionary<string, string> ToDictionary(this NSDictionary input) => input.ToDictionary(x => x.Key.ToString(), x => x.Value.ToString());
}
