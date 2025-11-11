using System.Reflection;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Bible.Alarm.Common.Extensions;

public static class CloneExtensions
{
    public static T DeepClone<T>(this T obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));
        
        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            ContractResolver = new IgnoreNonSerializableContractResolver()
        };
        var json = JsonConvert.SerializeObject(obj, settings);
        var result = JsonConvert.DeserializeObject<T>(json, settings);
        if (result == null)
            throw new InvalidOperationException("Deserialization returned null");
        
        return result;
    }

    private class IgnoreNonSerializableContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            
            // Ignore ICommand properties and other interfaces that can't be instantiated
            if (property.PropertyType == null) return property;
            if (!typeof(ICommand).IsAssignableFrom(property.PropertyType) &&
                !property.PropertyType.IsInterface) return property;
            property.ShouldSerialize = _ => false;
            property.Ignored = true;

            return property;
        }
    }
}