using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Windows.Input;

namespace Bible.Alarm.Common.Extensions;

public static class CloneExtensions
{
    public static T DeepClone<T>(this T obj)
    {
        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            ContractResolver = new IgnoreNonSerializableContractResolver()
        };
        var json = JsonConvert.SerializeObject(obj, settings);
        return JsonConvert.DeserializeObject<T>(json, settings);
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