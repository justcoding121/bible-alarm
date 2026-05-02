#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Effects;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class ScheduleEffectsOptionalDepsTests
{
    [Fact]
    public void ScheduleEffectsOptionalDeps_records_equal_when_all_optional_slots_match()
    {
        var state = new FakeState();

        var a = new ScheduleEffectsOptionalDeps(State: state);
        var b = new ScheduleEffectsOptionalDeps(State: state);

        Assert.Equal(a, b);
    }

    private sealed class FakeState : IState<ApplicationState>
    {
        public ApplicationState Value => new();

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }
}
