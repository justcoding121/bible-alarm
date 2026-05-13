#nullable enable

using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Tests;

public sealed class DeclaredRuntimeEventFinderTests
{
    private sealed class HostWithClickEvent
    {
        public event EventHandler? Clicked;
    }

    [Fact]
    public void TryLookup_returns_found_when_matching_runtime_event_exists()
    {
        var outcome = DeclaredRuntimeEventFinder.TryLookupDeclaredInstanceEvent(
            typeof(HostWithClickEvent),
            nameof(HostWithClickEvent.Clicked),
            out var info);

        Assert.Equal(DeclaredRuntimeEventLookupOutcome.Found, outcome);
        Assert.Equal(nameof(HostWithClickEvent.Clicked), info!.Name);
    }

    [Fact]
    public void TryLookup_returns_missing_named_event_when_runtime_events_exist_but_name_differs()
    {
        var outcome = DeclaredRuntimeEventFinder.TryLookupDeclaredInstanceEvent(
            typeof(HostWithClickEvent),
            eventName: "NotRegistered",
            out var info);

        Assert.Equal(DeclaredRuntimeEventLookupOutcome.MissingNamedEvent, outcome);
        Assert.Null(info);
    }

    [Fact]
    public void TryLookup_returns_no_declared_events_for_primitive_runtime_types()
    {
        var outcome = DeclaredRuntimeEventFinder.TryLookupDeclaredInstanceEvent(
            typeof(int),
            eventName: "anything",
            out var info);

        Assert.Equal(DeclaredRuntimeEventLookupOutcome.NoDeclaredEvents, outcome);
        Assert.Null(info);
    }
}
