#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Music;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests.ViewModels.Music;

public sealed class MusicPublicationSelectionViewModelTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class NopDispatcher : IDispatcher
    {
#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action)
        {
        }
    }

    private sealed class IdleLanguageNameService : ILanguageNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<int, string>());

        public string? GetNameCached(int languageId) => null;

        public string? GetNameByLanguageCodeCached(string languageCode) => null;
    }

    private sealed class ScopeNeverOpenedFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Headless publication selection tests must not open scopes.");
    }

    private static ScheduleStateItem MinimalSchedule(string? musicLangDirection = null) =>
        new()
        {
            Id = 1,
            Name = "Test",
            IsEnabled = true,
            Hour = 8,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
            MusicLanguageDirection = musicLangDirection,
        };

    private static MusicPublicationSelectionViewModelDeps CreateDeps(IState<ApplicationState> fluxorState)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMediaService>(new IdleCatalogMediaService());
        services.AddSingleton<ILanguageNameService>(new IdleLanguageNameService());
        services.AddSingleton<IServiceScopeFactory>(new ScopeNeverOpenedFactory());
        var provider = services.BuildServiceProvider();
        return new MusicPublicationSelectionViewModelDeps(
            provider.GetRequiredService<IMediaService>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            fluxorState,
            new NopDispatcher(),
            new UnusedNavigationServiceStub(),
            provider);
    }

    [Fact]
    public void ContentFlowDirection_maps_music_language_direction_right_to_left()
    {
        var schedule = MinimalSchedule(AppConstants.Media.TextDirectionRightToLeft);
        var fluxorState = new FakeApplicationState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule));

        using var sut = new MusicPublicationSelectionViewModel(CreateDeps(fluxorState));

        Assert.Equal(FlowDirection.RightToLeft, sut.ContentFlowDirection);
    }

    [Fact]
    public void ContentFlowDirection_defaults_to_left_to_right_when_direction_unspecified()
    {
        var schedule = MinimalSchedule(null);
        var fluxorState = new FakeApplicationState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule));

        using var sut = new MusicPublicationSelectionViewModel(CreateDeps(fluxorState));

        Assert.Equal(FlowDirection.LeftToRight, sut.ContentFlowDirection);
    }
}
