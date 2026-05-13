#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using AlarmMusic = Bible.Alarm.Shared.Models.Schedule.AlarmMusic;
using BiblePublicationSchedule = Bible.Alarm.Shared.Models.Schedule.BiblePublicationSchedule;

namespace Bible.Alarm.Tests.ViewModels.Schedule;

public sealed class BiblePublicationSelectionContainerViewModelTests
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

    private sealed class StubScheduleSelectionService : IScheduleSelectionService
    {
        public AlarmMusic? LoadMusicForSelection(LoadMusicForSelectionArgs args) => null;

        public BiblePublicationSchedule? LoadBiblePublicationForSelection(LoadBiblePublicationForSelectionArgs args) => null;

        public void Dispose()
        {
        }
    }

    private sealed class StubCategoryNameService : ICategoryNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string? GetName(string categoryCode, string displayLanguageCode) => categoryCode;
    }

    private sealed class ScopeNeverOpenedFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("BiblePublicationSelectionContainer headless tests must not open scopes.");
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMediaService>(new IdleCatalogMediaService());
        services.AddSingleton<ICategoryNameService>(new StubCategoryNameService());
        services.AddSingleton<IServiceScopeFactory>(new ScopeNeverOpenedFactory());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void CategoryDisplayText_is_empty_when_current_schedule_is_missing()
    {
        var fluxorState = new MutableApplicationState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), null));
        using var sut = new BiblePublicationSelectionContainerViewModel(
            TestLogging.CreateLogger(),
            new UnusedNavigationServiceStub(),
            new StubScheduleSelectionService(),
            fluxorState,
            new RecordingDispatcher(),
            CreateMapper(),
            CreateServiceProvider());

        Assert.Equal(string.Empty, sut.CategoryDisplayText);
    }
}
