using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Windows.Input;

namespace Bible.Alarm.Common.Extensions;

public static class CloneExtensions
{
    private static readonly JsonSerializerOptions Options = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        TypeInfoResolver = new IgnoreNonSerializableTypeInfoResolver()
    };

    public static T DeepClone<T>(this T obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        var json = JsonSerializer.Serialize(obj, Options);
        var result = JsonSerializer.Deserialize<T>(json, Options);
        if (result == null)
        {
            throw new InvalidOperationException("Deserialization returned null");
        }

        return result;
    }

    private class IgnoreNonSerializableTypeInfoResolver : IJsonTypeInfoResolver
    {
        private readonly DefaultJsonTypeInfoResolver _defaultResolver = new();

        public JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var typeInfo = _defaultResolver.GetTypeInfo(type, options);

            if (typeInfo.Kind == JsonTypeInfoKind.Object)
            {
                var propertiesToRemove = new List<JsonPropertyInfo>();

                foreach (var property in typeInfo.Properties)
                {
                    // Ignore ICommand properties and other interfaces that can't be instantiated
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
}