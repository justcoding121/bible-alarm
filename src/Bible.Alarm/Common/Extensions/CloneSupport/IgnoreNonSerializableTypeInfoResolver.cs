using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Windows.Input;

namespace Bible.Alarm.Common.Extensions;

/// <summary>
/// JsonTypeInfoResolver that ignores ICommand and interface properties during serialization.
/// Used by CloneExtensions for deep clone support.
/// </summary>
internal sealed class IgnoreNonSerializableTypeInfoResolver : IJsonTypeInfoResolver
{
    private readonly DefaultJsonTypeInfoResolver defaultResolver = new();

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var typeInfo = defaultResolver.GetTypeInfo(type, options);

        if (typeInfo != null && typeInfo.Kind == JsonTypeInfoKind.Object)
        {
            var propertiesToRemove = new List<JsonPropertyInfo>();

            foreach (var property in typeInfo.Properties)
            {
                if (property.PropertyType != null &&
                    (typeof(ICommand).IsAssignableFrom(property.PropertyType) ||
                     property.PropertyType.IsInterface))
                {
                    propertiesToRemove.Add(property);
                }
            }

            foreach (var property in propertiesToRemove)
            {
                typeInfo.Properties.Remove(property);
            }
        }

        return typeInfo;
    }
}
