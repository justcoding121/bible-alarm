#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class ScheduleEffectsMusicSectionPopulatorTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class UnexpectedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Scope must not be created when populator exits early.");
    }

    [Fact]
    public async Task PopulateMusicSectionNameForStateAsync_no_op_when_not_melody_publication()
    {
        var dispatcher = new RecordingDispatcher();
        await ScheduleEffectsMusicSectionPopulator.PopulateMusicSectionNameForStateAsync(
            new ScheduleStateItem
            {
                MusicPublicationCode = "vocal-non-melody-test",
                MusicTrackCode = "1",
                MusicSectionName = null,
            },
            dispatcher,
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task PopulateMusicSectionNameForStateAsync_no_op_when_melody_but_track_missing()
    {
        var dispatcher = new RecordingDispatcher();
        await ScheduleEffectsMusicSectionPopulator.PopulateMusicSectionNameForStateAsync(
            new ScheduleStateItem
            {
                MusicPublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                MusicTrackCode = "   ",
                MusicSectionName = null,
            },
            dispatcher,
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task PopulateMusicSectionNameForStateAsync_no_op_when_section_name_already_populated()
    {
        var dispatcher = new RecordingDispatcher();
        await ScheduleEffectsMusicSectionPopulator.PopulateMusicSectionNameForStateAsync(
            new ScheduleStateItem
            {
                MusicPublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                MusicTrackCode = "1",
                MusicSectionName = "Known section",
            },
            dispatcher,
            new UnexpectedScopeFactory(),
            TestLogging.CreateLogger());

        Assert.Empty(dispatcher.Dispatched);
    }
}
