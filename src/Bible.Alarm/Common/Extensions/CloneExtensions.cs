using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bible.Alarm.Common.Extensions;

public static class CloneExtensions
{
    private static readonly JsonSerializerOptions options = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        TypeInfoResolver = new IgnoreNonSerializableTypeInfoResolver()
    };

    public static T DeepClone<T>(this T obj)
    {
        if (EqualityComparer<T>.Default.Equals(obj, default(T)))
        {
            throw new ArgumentNullException(nameof(obj));
        }

        var json = JsonSerializer.Serialize(obj, options);
        var result = JsonSerializer.Deserialize<T>(json, options) ?? throw new InvalidOperationException("Deserialization returned null");
        return result;
    }
}
