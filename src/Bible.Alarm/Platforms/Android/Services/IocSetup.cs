using Android.App;
using Android.Content;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Bible.Alarm.Services.Droid.Extensions;

namespace Bible.Alarm.Services.Droid
{
    public class IocSetup
    {
        private static ConcurrentDictionary<object, IContainer> containers
            = new ConcurrentDictionary<object, IContainer>();

        public static IContainer GetContainer()
        {
            var container = containers.OrderBy(x => x.Value.IsAndroidService() ? 1 : 0).FirstOrDefault();

            if (!container.Equals(default(KeyValuePair<object, IContainer>)))
            {
                return container.Value;
            }

            throw new Exception("No containers found.");
        }

        public static void Remove(object key)
        {
            containers.TryRemove(key, out var _);
        }

        public static Tuple<IContainer, bool> Initialize(
            Context androidContext,
            bool isService)
        {
            if (containers.TryGetValue(androidContext, out var existing))
            {
                return new Tuple<IContainer, bool>(existing, false);
            }

            var context = new Dictionary<string, object>
            {
                { "AndroidContext", androidContext },
                { "IsAndroidService", isService }
            };

            IContainer container = new Container(context);

            // Initialize other IocSetups
            // UI.IocSetup.Initialize(container, isService);
            // Alarm.Services.IocSetup.Initialize(container, isService);
            // Alarm.Services.Droid.IocSetup.Initialize(container, isService);
            // ViewModels.IocSetup.Initialize(container, isService);

            container = containers.GetOrAdd(androidContext, container);

            return new Tuple<IContainer, bool>(container, true);
        }
    }
}