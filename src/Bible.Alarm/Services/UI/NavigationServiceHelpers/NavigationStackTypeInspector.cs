#nullable enable

using System.Collections;

namespace Bible.Alarm.Services.UI.NavigationServiceHelpers;

internal static class NavigationStackTypeInspector
{
    internal static bool ContainsPageWithRuntimeType(IEnumerable navigationStack, Type expectedPageType)
    {
        foreach (var p in navigationStack)
        {
            if (p?.GetType() == expectedPageType)
            {
                return true;
            }
        }

        return false;
    }
}
