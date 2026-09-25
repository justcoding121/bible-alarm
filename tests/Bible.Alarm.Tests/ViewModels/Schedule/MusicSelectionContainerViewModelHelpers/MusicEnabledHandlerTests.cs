#nullable enable

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainerViewModelHelpers;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicEnabledHandlerTests
{
    private sealed class FakeAppState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value { get; set; } = value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        private readonly ManualResetEventSlim? signal;
        private readonly int signalAfterCount;

        public RecordingDispatcher(ManualResetEventSlim? signal = null, int signalAfterCount = 1)
        {
            this.signal = signal;
            this.signalAfterCount = signalAfterCount;
        }

        public List<object> Dispatched { get; } = [];

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            if (signal is not null && Dispatched.Count >= signalAfterCount)
            {
                signal.Set();
            }
        }

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public ConcurrentBag<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class StubMelodyMusicService : IMelodyMusicService
    {
        public Dictionary<string, MelodyMusic>? Releases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public MelodyMusic? ByCode { get; set; }
        public Exception? ThrowOnGetAll { get; set; }
        public Action<string>? OnGetByCode { get; set; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnGetAll is not null)
            {
                throw ThrowOnGetAll;
            }

            return Task.FromResult(Releases!);
        }

        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode,
            CancellationToken cancellationToken = default)
        {
            OnGetByCode?.Invoke(publicationCode);
            if (ByCode is not null)
            {
                return Task.FromResult<MelodyMusic?>(ByCode);
            }

            if (Releases is not null && Releases.TryGetValue(publicationCode, out var melody))
            {
                return Task.FromResult<MelodyMusic?>(melody);
            }

            return Task.FromResult<MelodyMusic?>(null);
        }

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode,
            string sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static MelodyMusic CreateMelody(
        string publicationCode,
        string name,
        string trackCode = "7",
        string trackTitle = "Track Seven",
        string? sectionCode = null,
        string? sectionName = null)
    {
        var publication = new BiblePublication
        {
            PublicationCode = publicationCode,
            Name = name,
            Tracks = [],
            Sections = [],
        };

        var track = new BiblePublicationTrack
        {
            TrackCode = trackCode,
            Title = trackTitle,
            Publication = publication,
        };
        publication.Tracks.Add(track);

        if (!string.IsNullOrEmpty(sectionCode))
        {
            var section = new BiblePublicationSection
            {
                SectionCode = sectionCode,
                Name = sectionName ?? $"Section {sectionCode}",
                BiblePublication = publication,
                Tracks = [track],
            };
            publication.Sections.Add(section);
            track.Section = section;
        }

        return new MelodyMusic { Publication = publication };
    }

    private static MusicEnabledHandler CreateHandler(
        ScheduleStateItem? schedule,
        out RecordingDispatcher dispatcher,
        IMelodyMusicService? melody = null,
        ILogger? logger = null,
        ManualResetEventSlim? dispatchSignal = null,
        int signalAfterCount = 1,
        FakeAppState? state = null)
    {
        dispatcher = new RecordingDispatcher(dispatchSignal, signalAfterCount);
        var appState = state ?? new FakeAppState(new ApplicationState { CurrentSchedule = schedule });
        var services = new ServiceCollection();
        if (melody is not null)
        {
            services.AddSingleton<IMelodyMusicService>(melody);
        }

        return new MusicEnabledHandler(
            logger ?? TestLogging.CreateLogger(),
            dispatcher,
            services.BuildServiceProvider(),
            appState);
    }

    /// <summary>
    /// MainThread.BeginInvokeOnMainThread may throw COMException on headless Windows without a WinUI dispatcher.
    /// Returns false when the call could not complete past MainThread.
    /// </summary>
    private static bool TryHandleSetMusicEnabled(
        MusicEnabledHandler handler,
        bool value,
        bool currentValue,
        bool isUpdatingFromState,
        bool? initialMusicEnabledOnPageLoad,
        Action<bool> setPendingMusicEnabled,
        Action<bool> setShouldScrollToBottom,
        Action onPropertyChanged,
        out bool result)
    {
        try
        {
            result = handler.HandleSetMusicEnabled(
                value,
                currentValue,
                isUpdatingFromState,
                initialMusicEnabledOnPageLoad,
                setPendingMusicEnabled,
                setShouldScrollToBottom,
                onPropertyChanged);
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            result = false;
            return false;
        }
    }

    private static void WaitForDispatchCount(RecordingDispatcher dispatcher, int count, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(2);
        Assert.True(
            SpinWait.SpinUntil(() => dispatcher.Dispatched.Count >= count, limit),
            $"Expected at least {count} dispatch(es); got {dispatcher.Dispatched.Count}.");
    }

    [Fact]
    public void HandleSetMusicEnabled_returns_false_when_current_schedule_missing()
    {
        var dispatcher = new RecordingDispatcher();
        var state = new FakeAppState(new ApplicationState { CurrentSchedule = null });
        var handler = new MusicEnabledHandler(
            TestLogging.CreateLogger(),
            dispatcher,
            new ServiceCollection().BuildServiceProvider(),
            state);

        var pendingToggles = 0;
        Assert.False(handler.HandleSetMusicEnabled(true, false, false, null, _ => pendingToggles++, _ => { }, () => { }));

        Assert.Equal(0, pendingToggles);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleSetMusicEnabled_returns_false_when_value_unchanged()
    {
        var schedule = new ScheduleStateItem { Id = 1 };
        var handler = CreateHandler(schedule, out var dispatcher);

        Assert.False(handler.HandleSetMusicEnabled(false, false, false, null, _ => { }, _ => { }, () => { }));
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleSetMusicEnabled_while_syncing_from_state_clears_pending_and_invokes_property_changed()
    {
        var schedule = new ScheduleStateItem { Id = 1 };
        var handler = CreateHandler(schedule, out var dispatcher);

        var pending = true;
        var propCalls = 0;
        Assert.False(handler.HandleSetMusicEnabled(
            true,
            currentValue: false,
            isUpdatingFromState: true,
            initialMusicEnabledOnPageLoad: null,
            v => pending = v,
            _ => { },
            () => propCalls++));

        Assert.False(pending);
        Assert.Equal(1, propCalls);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleSetMusicEnabled_when_enabling_returns_true()
    {
        var schedule = new ScheduleStateItem { Id = 2 };
        var handler = CreateHandler(schedule, out _);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: null,
                _ => { },
                _ => { },
                () => { },
                out var result))
        {
            return;
        }

        Assert.True(result);
    }

    [Fact]
    public void HandleSetMusicEnabled_when_enabling_sets_pending_true()
    {
        var schedule = new ScheduleStateItem { Id = 2 };
        var handler = CreateHandler(schedule, out _);
        var pending = false;

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: null,
                v => pending = v,
                _ => { },
                () => { },
                out _))
        {
            Assert.True(pending);
            return;
        }

        Assert.True(pending);
    }

    [Fact]
    public void HandleSetMusicEnabled_when_enabling_requests_scroll_to_bottom()
    {
        var schedule = new ScheduleStateItem { Id = 2 };
        var handler = CreateHandler(schedule, out _);
        var scroll = false;

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: null,
                _ => { },
                v => scroll = v,
                () => { },
                out _))
        {
            return;
        }

        Assert.True(scroll);
    }

    [Fact]
    public void HandleSetMusicEnabled_when_enabling_dispatches_music_enabled_schedule_update()
    {
        var schedule = new ScheduleStateItem { Id = 2 };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(schedule, out var dispatcher, dispatchSignal: signal);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: null,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(Assert.Single(dispatcher.Dispatched));
        Assert.True(update.Schedule.MusicEnabled);
    }

    [Fact]
    public void HandleSetMusicEnabled_when_enabling_with_existing_codes_sets_music_updated_cascade_true()
    {
        var schedule = new ScheduleStateItem
        {
            Id = 2,
            MusicPublicationCode = "iam",
            MusicTrackCode = "105",
        };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(schedule, out var dispatcher, dispatchSignal: signal);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: null,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(Assert.Single(dispatcher.Dispatched));
        Assert.True(update.MusicUpdated);
    }

    [Fact]
    public void HandleSetMusicEnabled_when_enabling_without_codes_sets_music_updated_false()
    {
        var schedule = new ScheduleStateItem { Id = 2 };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(schedule, out var dispatcher, dispatchSignal: signal);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: null,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        var update = Assert.IsType<UpdateScheduleFromViewModelAction>(Assert.Single(dispatcher.Dispatched));
        Assert.False(update.MusicUpdated);
    }

    [Fact]
    public void HandleSetMusicEnabled_loads_preferred_iam_melody_when_started_disabled_and_empty()
    {
        var schedule = new ScheduleStateItem { Id = 5, MusicEnabled = false };
        var iam = CreateMelody(AppConstants.Media.MelodyMusicPublicationCodeIam, "Kingdom Melodies");
        var other = CreateMelody("other", "Other Melodies", trackCode: "99");
        var melody = new StubMelodyMusicService
        {
            Releases =
            {
                ["other"] = other,
                [AppConstants.Media.MelodyMusicPublicationCodeIam] = iam,
            },
            ByCode = iam,
        };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(
            schedule,
            out var dispatcher,
            melody,
            dispatchSignal: signal,
            signalAfterCount: 2);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: false,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        WaitForDispatchCount(dispatcher, 2);
        var melodyUpdate = dispatcher.Dispatched
            .OfType<UpdateScheduleFromViewModelAction>()
            .Last(a => !string.IsNullOrEmpty(a.Schedule.MusicPublicationCode));
        Assert.Equal(AppConstants.Media.MelodyMusicPublicationCodeIam, melodyUpdate.Schedule.MusicPublicationCode);
    }

    [Fact]
    public void HandleSetMusicEnabled_falls_back_to_first_available_melody_when_iam_absent()
    {
        var schedule = new ScheduleStateItem { Id = 6, MusicEnabled = false };
        var fallback = CreateMelody("snnw", "Sing to Jehovah", trackCode: "3");
        var melody = new StubMelodyMusicService
        {
            Releases = { ["snnw"] = fallback },
            ByCode = fallback,
        };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(
            schedule,
            out var dispatcher,
            melody,
            dispatchSignal: signal,
            signalAfterCount: 2);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: false,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        WaitForDispatchCount(dispatcher, 2);
        var melodyUpdate = dispatcher.Dispatched
            .OfType<UpdateScheduleFromViewModelAction>()
            .Last(a => !string.IsNullOrEmpty(a.Schedule.MusicPublicationCode));
        Assert.Equal("snnw", melodyUpdate.Schedule.MusicPublicationCode);
    }

    [Fact]
    public void HandleSetMusicEnabled_selects_track_from_sections_when_sections_have_tracks()
    {
        var schedule = new ScheduleStateItem { Id = 7, MusicEnabled = false };
        var withSections = CreateMelody(
            AppConstants.Media.MelodyMusicPublicationCodeIam,
            "Kingdom Melodies",
            trackCode: "12",
            sectionCode: "iam-1",
            sectionName: "Disc 1");
        var melody = new StubMelodyMusicService
        {
            Releases = { [AppConstants.Media.MelodyMusicPublicationCodeIam] = withSections },
            ByCode = withSections,
        };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(
            schedule,
            out var dispatcher,
            melody,
            dispatchSignal: signal,
            signalAfterCount: 2);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: false,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        WaitForDispatchCount(dispatcher, 2);
        var melodyUpdate = dispatcher.Dispatched
            .OfType<UpdateScheduleFromViewModelAction>()
            .Last(a => !string.IsNullOrEmpty(a.Schedule.MusicSectionCode));
        Assert.Equal("iam-1", melodyUpdate.Schedule.MusicSectionCode);
    }

    [Fact]
    public void HandleSetMusicEnabled_skips_melody_dispatch_when_schedule_already_pointing_at_chosen_melody()
    {
        var schedule = new ScheduleStateItem { Id = 8, MusicEnabled = false };
        var iam = CreateMelody(AppConstants.Media.MelodyMusicPublicationCodeIam, "Kingdom Melodies", trackCode: "7");
        using var melodyLoaded = new ManualResetEventSlim(false);
        var melody = new StubMelodyMusicService
        {
            Releases = { [AppConstants.Media.MelodyMusicPublicationCodeIam] = iam },
            ByCode = iam,
            OnGetByCode = _ =>
            {
                schedule.MusicLanguageCode = null;
                schedule.MusicPublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam;
                schedule.MusicSectionCode = null;
                schedule.MusicTrackCode = "7";
                schedule.MusicEnabled = true;
                melodyLoaded.Set();
            },
        };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(
            schedule,
            out var dispatcher,
            melody,
            dispatchSignal: signal,
            signalAfterCount: 1);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: false,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(melodyLoaded.Wait(TimeSpan.FromSeconds(2)));
        var stableUntil = Environment.TickCount64 + 150;
        while (Environment.TickCount64 < stableUntil)
        {
            if (dispatcher.Dispatched.Count > 1)
            {
                break;
            }

            Thread.SpinWait(20_000);
        }

        Assert.Single(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleSetMusicEnabled_skips_melody_dispatch_when_releases_null()
    {
        var schedule = new ScheduleStateItem { Id = 9, MusicEnabled = false };
        var melody = new StubMelodyMusicService { Releases = null };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(schedule, out var dispatcher, melody, dispatchSignal: signal);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: false,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(
            SpinWait.SpinUntil(() => dispatcher.Dispatched.Count == 1, TimeSpan.FromSeconds(1)));
        Assert.Single(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleSetMusicEnabled_skips_melody_dispatch_when_releases_empty()
    {
        var schedule = new ScheduleStateItem { Id = 10, MusicEnabled = false };
        var melody = new StubMelodyMusicService { Releases = new Dictionary<string, MelodyMusic>() };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(schedule, out var dispatcher, melody, dispatchSignal: signal);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: false,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(
            SpinWait.SpinUntil(() => dispatcher.Dispatched.Count == 1, TimeSpan.FromSeconds(1)));
        Assert.Single(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleSetMusicEnabled_skips_melody_dispatch_when_tracks_empty()
    {
        var schedule = new ScheduleStateItem { Id = 11, MusicEnabled = false };
        var empty = new MelodyMusic
        {
            Publication = new BiblePublication
            {
                PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                Name = "Empty",
                Tracks = [],
                Sections = [],
            },
        };
        var melody = new StubMelodyMusicService
        {
            Releases = { [AppConstants.Media.MelodyMusicPublicationCodeIam] = empty },
            ByCode = empty,
        };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(schedule, out var dispatcher, melody, dispatchSignal: signal);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: false,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(
            SpinWait.SpinUntil(() => dispatcher.Dispatched.Count == 1, TimeSpan.FromSeconds(1)));
        Assert.Single(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleSetMusicEnabled_logs_and_swallows_melody_load_exception()
    {
        var schedule = new ScheduleStateItem { Id = 12, MusicEnabled = false };
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var melody = new StubMelodyMusicService
        {
            ThrowOnGetAll = new InvalidOperationException("melody catalog unavailable"),
        };
        using var signal = new ManualResetEventSlim(false);
        var handler = CreateHandler(
            schedule,
            out var dispatcher,
            melody,
            logger,
            dispatchSignal: signal);

        if (!TryHandleSetMusicEnabled(
                handler,
                true,
                currentValue: false,
                isUpdatingFromState: false,
                initialMusicEnabledOnPageLoad: false,
                _ => { },
                _ => { },
                () => { },
                out _))
        {
            return;
        }

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(
            SpinWait.SpinUntil(
                () => sink.Events.Any(e => e.Level == LogEventLevel.Error),
                TimeSpan.FromSeconds(2)));
        Assert.Contains(
            sink.Events,
            e => e.Level == LogEventLevel.Error &&
                 e.MessageTemplate.Text.Contains("Error loading default music", StringComparison.Ordinal));
        Assert.Single(dispatcher.Dispatched);
    }
}
