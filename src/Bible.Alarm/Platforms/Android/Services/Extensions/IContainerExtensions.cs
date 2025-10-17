using Android.Content;
using Bible.Alarm.Services.Contracts;
using System;

namespace Bible.Alarm.Services.Droid.Extensions
{
    public static class IContainerExtensions
    {
        public static Context AndroidContext(this IContainer container)
        {
            return container.Resolve<Context>();
        }

        public static bool IsAndroidService(this IContainer container)
        {
            return container.Resolve<Context>() != null;
        }
    }
}
