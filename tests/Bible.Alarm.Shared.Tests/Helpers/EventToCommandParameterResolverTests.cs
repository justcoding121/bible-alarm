#nullable enable

using System.Globalization;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class EventToCommandParameterResolverTests
{
    private sealed class SampleArgs : EventArgs;

    [Fact]
    public void Resolve_keeps_explicit_command_parameter_even_when_event_args_are_present()
    {
        var ea = new SampleArgs();
        var resolved = EventToCommandParameterResolver.Resolve(
            commandParameter: "bound-parameter",
            eventArgs: ea,
            convertEventArgs: (_, _, _) => "converted-should-not-run",
            converterParameter: null,
            culture: CultureInfo.InvariantCulture);

        Assert.Equal("bound-parameter", resolved);
    }

    [Fact]
    public void Resolve_falls_back_to_event_args_when_command_parameter_null_and_args_are_meaningful()
    {
        var ea = new SampleArgs();
        var resolved = EventToCommandParameterResolver.Resolve(null, ea, convertEventArgs: null, converterParameter: null,
            CultureInfo.InvariantCulture);

        Assert.Same(ea, resolved);
    }

    [Fact]
    public void Resolve_leaves_parameter_null_when_event_args_are_EventArgs_Empty()
    {
        var resolved = EventToCommandParameterResolver.Resolve(null, EventArgs.Empty, convertEventArgs: null,
            converterParameter: null,
            CultureInfo.InvariantCulture);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_applies_converter_when_binding_parameter_from_non_empty_event_args()
    {
        var ea = new SampleArgs();
        var resolved = EventToCommandParameterResolver.Resolve(
            null,
            ea,
            (_, _, _) => 42,
            converterParameter: null,
            CultureInfo.InvariantCulture);

        Assert.Equal(42, resolved);
    }
}
