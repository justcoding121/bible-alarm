#nullable enable

using System.Linq;
using System.Reflection;

namespace Bible.Alarm.Common.ViewHelpers;

internal enum DeclaredRuntimeEventLookupOutcome
{
    NoDeclaredEvents,
    MissingNamedEvent,
    Found,
}

internal static class DeclaredRuntimeEventFinder
{
    internal static DeclaredRuntimeEventLookupOutcome TryLookupDeclaredInstanceEvent(
        Type attachedType,
        string eventName,
        out EventInfo? eventInfo)
    {
        var events = attachedType.GetRuntimeEvents().ToArray();
        if (events.Length == 0)
        {
            eventInfo = null;
            return DeclaredRuntimeEventLookupOutcome.NoDeclaredEvents;
        }

        eventInfo = events.FirstOrDefault(e =>
            string.Equals(e.Name, eventName, StringComparison.Ordinal));

        return eventInfo == null
            ? DeclaredRuntimeEventLookupOutcome.MissingNamedEvent
            : DeclaredRuntimeEventLookupOutcome.Found;
    }
}
