#nullable enable

using AutoMapper;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests.ViewModels.Schedule;

public sealed class NumberOfTrackContainerViewModelTests
{
    private sealed class MutableApplicationState : IState<ApplicationState>
    {
        public MutableApplicationState(ApplicationState value) => Value = value;

        public ApplicationState Value { get; set; }

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : Fluxor.IDispatcher
    {
        public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Dispatched.Add(action);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static NumberOfTrackContainerViewModel CreateSut(RecordingDispatcher dispatcher)
    {
        var fluxorState = new MutableApplicationState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), null));
        return new NumberOfTrackContainerViewModel(
            TestLogging.CreateLogger(),
            new UnusedNavigationServiceStub(),
            new EmptyServiceProvider(),
            fluxorState,
            dispatcher,
            CreateMapper());
    }

    [Fact]
    public void ToggleAlwaysPlayFromStart_flips_locally_without_dispatch_when_schedule_is_missing()
    {
        var dispatcher = new RecordingDispatcher();
        using var sut = CreateSut(dispatcher);

        Assert.IsType<RelayCommand>(sut.ToggleAlwaysPlayFromStartCommand).Execute(null);
        Assert.True(sut.AlwaysPlayFromStart);

        Assert.IsType<RelayCommand>(sut.ToggleAlwaysPlayFromStartCommand).Execute(null);
        Assert.False(sut.AlwaysPlayFromStart);

        Assert.Empty(dispatcher.Dispatched);
    }
}
