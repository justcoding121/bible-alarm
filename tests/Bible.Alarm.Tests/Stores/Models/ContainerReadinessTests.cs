#nullable enable

using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class ContainerReadinessTests
{
    [Fact]
    public void AllReady_is_false_when_any_container_not_ready()
    {
        var sut = ContainerReadiness.AllContainersReady with { MusicSelection = false };

        Assert.False(sut.AllReady);
    }

    [Fact]
    public void AllReady_is_true_only_when_every_flag_true()
    {
        var sut = new ContainerReadiness
        {
            BiblePublicationSelection = true,
            MusicSelection = true,
            NumberOfTrack = true,
            ScheduleDetails = true,
            AlarmSettings = true,
        };

        Assert.True(sut.AllReady);
    }

    [Fact]
    public void NotReady_factory_has_all_flags_false()
    {
        var sut = ContainerReadiness.NotReady;

        Assert.False(sut.BiblePublicationSelection);
        Assert.False(sut.MusicSelection);
        Assert.False(sut.NumberOfTrack);
        Assert.False(sut.ScheduleDetails);
        Assert.False(sut.AlarmSettings);
        Assert.False(sut.AllReady);
    }
}
