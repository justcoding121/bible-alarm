using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Windows.Input;

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

    private class IgnoreNonSerializableTypeInfoResolver : IJsonTypeInfoResolver
    {
        private readonly DefaultJsonTypeInfoResolver defaultResolver = new();

        public JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var typeInfo = defaultResolver.GetTypeInfo(type, options);

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
