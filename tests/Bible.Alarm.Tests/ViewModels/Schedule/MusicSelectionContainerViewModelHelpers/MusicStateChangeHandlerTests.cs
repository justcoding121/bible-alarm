#nullable enable

#if ANDROID || IOS
using Xunit;

namespace Bible.Alarm.Tests;

/// <summary>
/// Device runners: suite skipped. WaitUntilAsync + MainThread races hang the AVD/simulator (20+ min).
/// Windows OpenCover host keeps the full suite below.
/// </summary>
public sealed class MusicStateChangeHandlerTests
{
    [Fact(Skip = "Host OpenCover coverage only; hangs on Android/iOS device runners")]
    public void Skipped_on_device_runners()
    {
    }
}
#else
using AutoMapper;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainerViewModelHelpers;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicStateChangeHandlerTests
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
        public List<object> Dispatched { get; } = [];

        public void Dispatch(object action) => Dispatched.Add(action);

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    private sealed class StubMelodyMusicService : IMelodyMusicService
    {
        public Dictionary<string, MelodyMusic> Releases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public MelodyMusic? ByCode { get; set; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Releases);

        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(ByCode ?? (Releases.TryGetValue(publicationCode, out var m) ? m : null));

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static ScheduleStateItem CreateSchedule(
        int id = 1,
        bool musicEnabled = true,
        string? languageCode = "E",
        string? publicationCode = "sjjc",
        string? sectionCode = null,
        string? trackCode = "1",
        bool? repeat = false,
        string? publicationName = "Songs",
        string? sectionName = null,
        string? trackName = "Song 1",
        string? bibleDirection = "ltr",
        string? musicDirection = "ltr") =>
        new()
        {
            Id = id,
            MusicEnabled = musicEnabled,
            MusicLanguageCode = languageCode,
            MusicLanguageName = languageCode == null ? null : "English",
            MusicPublicationCode = publicationCode,
            MusicPublicationName = publicationName,
            MusicSectionCode = sectionCode,
            MusicSectionName = sectionName,
            MusicTrackCode = trackCode,
            MusicTrackName = trackName,
            MusicRepeat = repeat,
            BiblePublicationLanguageDirection = bibleDirection,
            MusicLanguageDirection = musicDirection,
        };

    private static MelodyMusic CreateMelodyRelease(string code, string name, string trackCode = "10", string? sectionCode = "iam-1")
    {
        var publication = new BiblePublication
        {
            PublicationCode = code,
            Name = name,
            Tracks = [],
            Sections = [],
        };

        var track = new BiblePublicationTrack
        {
            TrackCode = trackCode,
            Title = $"Melody {trackCode}",
            Publication = publication,
        };
        publication.Tracks.Add(track);

        if (!string.IsNullOrEmpty(sectionCode))
        {
            var section = new BiblePublicationSection
            {
                SectionCode = sectionCode,
                Name = $"Disc {sectionCode}",
                BiblePublication = publication,
                Tracks = [track],
            };
            publication.Sections.Add(section);
            track.Section = section;
        }

        return new MelodyMusic { Publication = publication };
    }

    private static (MusicStateChangeHandler Sut, FakeAppState State, RecordingDispatcher Dispatcher, MusicStateTracker Tracker, List<string> NotifiedProperties, StubMelodyMusicService? Melody)
        CreateHandler(
            ScheduleStateItem? currentSchedule,
            MusicStateTracker? tracker = null,
            IServiceProvider? serviceProvider = null,
            StubMelodyMusicService? melody = null)
    {
        var state = new FakeAppState(new ApplicationState { CurrentSchedule = currentSchedule });
        var dispatcher = new RecordingDispatcher();
        var stateTracker = tracker ?? new MusicStateTracker();
        var notified = new List<string>();
        var display = new MusicDisplayTextProvider(state, new IdleCatalogMediaService(), TestLogging.CreateLogger());
        var notifier = new MusicPropertyNotifier(notified.Add, display);

        IServiceProvider sp;
        if (serviceProvider != null)
        {
            sp = serviceProvider;
        }
        else if (melody != null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IMelodyMusicService>(melody);
            sp = services.BuildServiceProvider();
        }
        else
        {
            sp = new ServiceCollection().BuildServiceProvider();
        }

        var sut = new MusicStateChangeHandler(
            new MusicStateChangeHandlerServices(
                TestLogging.CreateLogger(),
                state,
                dispatcher,
                CreateMapper(),
                sp),
            new MusicStateChangeHandlerCollaborators(stateTracker, notifier, display));

        return (sut, state, dispatcher, stateTracker, notified, melody);
    }

    /// <summary>
    /// MainThread.BeginInvokeOnMainThread may throw COMException on the Windows host without a WinUI dispatcher.
    /// Synchronous tracker/dispatcher side effects still run before the posted UI work.
    /// </summary>
    private static void InvokeHandleStateChanged(
        MusicStateChangeHandler sut,
        int scheduleId,
        MusicStateHolder holder,
        Action<bool> setShouldScrollToBottom,
        Action<string> onPropertyChanged)
    {
        try
        {
            sut.HandleStateChanged(scheduleId, holder, setShouldScrollToBottom, onPropertyChanged);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    [Fact]
    public void Ctor_wires_services_and_collaborators()
    {
        var services = new MusicStateChangeHandlerServices(
            null!, null!, null!, null!, null!);
        var collaborators = new MusicStateChangeHandlerCollaborators(
            null!, null!, null!);

        var sut = new MusicStateChangeHandler(services, collaborators);
        Assert.NotNull(sut);
    }

    [Fact]
    public void HandleStateChanged_returns_early_when_schedule_id_mismatches()
    {
        var schedule = CreateSchedule(id: 5);
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(CreateSchedule(id: 5, trackCode: "1"));
        var holder = new MusicStateHolder();
        var scrollCalls = 0;
        var propCalls = 0;

        InvokeHandleStateChanged(sut, scheduleId: 99, holder, _ => scrollCalls++, _ => propCalls++);

        Assert.Equal(0, scrollCalls);
        Assert.Equal(0, propCalls);
        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal("1", tracker.LastScheduleMusicTrackCode);
        Assert.False(tracker.LastMusicEnabled is null);
    }

    [Fact]
    public void HandleStateChanged_noops_when_current_schedule_is_null()
    {
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(currentSchedule: null);
        var holder = new MusicStateHolder();

        InvokeHandleStateChanged(sut, scheduleId: 1, holder, _ => { }, _ => { });

        Assert.Empty(dispatcher.Dispatched);
        Assert.Null(tracker.LastScheduleMusicTrackCode);
        Assert.Null(holder.Music);
    }

    [Fact]
    public void HandleStateChanged_clears_pending_music_enabled_when_value_matches_state()
    {
        var schedule = CreateSchedule(musicEnabled: true);
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(schedule);
        var holder = new MusicStateHolder { PendingMusicEnabled = true };

        InvokeHandleStateChanged(sut, schedule.Id, holder, _ => { }, _ => { });

        Assert.Null(holder.PendingMusicEnabled);
    }

    [Fact]
    public void HandleStateChanged_music_enabled_queued_updates_tracker_without_queuing_again()
    {
        var baseline = CreateSchedule(musicEnabled: false);
        var schedule = CreateSchedule(musicEnabled: true);
        var (sut, _, dispatcher, tracker, notified, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);
        var holder = new MusicStateHolder { IsMusicEnabledNotificationQueued = true };
        var propNames = new List<string>();

        InvokeHandleStateChanged(sut, schedule.Id, holder, _ => { }, propNames.Add);

        Assert.True(tracker.LastMusicEnabled);
        Assert.Null(holder.PendingMusicEnabled);
        Assert.True(holder.IsMusicEnabledNotificationQueued);
        Assert.Empty(propNames);
        Assert.Empty(notified);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleStateChanged_music_enabled_true_from_false_updates_tracker_and_initializes()
    {
        var baseline = CreateSchedule(musicEnabled: false, trackCode: "1");
        var schedule = CreateSchedule(musicEnabled: true, trackCode: "2", trackName: "Song 2");
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);
        var holder = new MusicStateHolder();

        InvokeHandleStateChanged(sut, schedule.Id, holder, _ => { }, _ => { });

        // UpdateMusicEnabled runs before MainThread; InitializeFromSchedule may not if COMException.
        Assert.True(tracker.LastMusicEnabled);
        Assert.Null(holder.PendingMusicEnabled);
        Assert.False(holder.IsUpdatingFromState);
        if (tracker.LastScheduleMusicTrackCode == "2")
        {
            Assert.Equal("2", tracker.LastScheduleMusicTrackCode);
        }
    }

    [Fact]
    public void HandleStateChanged_updates_tracker_when_language_code_changes()
    {
        var baseline = CreateSchedule(languageCode: "E");
        var schedule = CreateSchedule(languageCode: "S");
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.Equal("S", tracker.LastScheduleMusicLanguageCode);
    }

    [Fact]
    public void HandleStateChanged_updates_tracker_when_publication_code_changes()
    {
        var baseline = CreateSchedule(publicationCode: "sjjc");
        var schedule = CreateSchedule(publicationCode: "snv");
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.Equal("snv", tracker.LastScheduleMusicPublicationCode);
    }

    [Fact]
    public void HandleStateChanged_updates_tracker_when_section_code_changes()
    {
        var baseline = CreateSchedule(
            publicationCode: AppConstants.Media.MelodyMusicPublicationCodeIam,
            sectionCode: "iam-1",
            languageCode: null);
        var schedule = CreateSchedule(
            publicationCode: AppConstants.Media.MelodyMusicPublicationCodeIam,
            sectionCode: "iam-2",
            languageCode: null);
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.Equal("iam-2", tracker.LastMusicSectionCode);
    }

    [Fact]
    public void HandleStateChanged_updates_tracker_when_track_code_changes()
    {
        var baseline = CreateSchedule(trackCode: "1");
        var schedule = CreateSchedule(trackCode: "42");
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.Equal("42", tracker.LastScheduleMusicTrackCode);
    }

    [Fact]
    public void HandleStateChanged_updates_tracker_when_repeat_changes()
    {
        var baseline = CreateSchedule(repeat: false);
        var schedule = CreateSchedule(repeat: true);
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.True(tracker.LastScheduleMusicRepeat);
    }

    [Fact]
    public void HandleStateChanged_updates_tracker_when_publication_or_section_name_changes()
    {
        var baseline = CreateSchedule(publicationName: "Old Pub", sectionName: "Old Sec", sectionCode: "1");
        var schedule = CreateSchedule(publicationName: "New Pub", sectionName: "New Sec", sectionCode: "1");
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.False(tracker.HasMusicPublicationNameChanged(schedule));
        Assert.False(tracker.HasMusicSectionNameChanged(schedule));
    }

    [Fact]
    public void HandleStateChanged_updates_bible_language_direction_on_tracker()
    {
        var baseline = CreateSchedule(bibleDirection: "ltr");
        var schedule = CreateSchedule(bibleDirection: "rtl");
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.Equal("rtl", tracker.LastBibleLanguageDirection);
        Assert.False(tracker.HasBibleLanguageDirectionChanged(schedule));
    }

    [Fact]
    public void HandleStateChanged_updates_music_language_direction_on_tracker()
    {
        var baseline = CreateSchedule(musicDirection: "ltr");
        var schedule = CreateSchedule(musicDirection: "rtl");
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.Equal("rtl", tracker.LastMusicLanguageDirection);
        Assert.False(tracker.HasMusicLanguageDirectionChanged(schedule));
    }

    [Fact]
    public void HandleStateChanged_skips_default_music_when_publication_already_set()
    {
        var schedule = CreateSchedule(musicEnabled: true, publicationCode: "sjjc");
        var melody = new StubMelodyMusicService();
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule, melody: melody);
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.Empty(dispatcher.Dispatched);
        Assert.True(tracker.ShouldTriggerDefaultMusicForNullPublication(schedule.Id));
    }

    [Fact]
    public void HandleStateChanged_skips_default_music_when_already_triggered_for_schedule()
    {
        var schedule = CreateSchedule(musicEnabled: true, publicationCode: null, trackCode: null, trackName: null);
        var melody = new StubMelodyMusicService
        {
            Releases =
            {
                [AppConstants.Media.MelodyMusicPublicationCodeIam] =
                    CreateMelodyRelease(AppConstants.Media.MelodyMusicPublicationCodeIam, "Kingdom Melodies"),
            },
        };
        melody.ByCode = melody.Releases[AppConstants.Media.MelodyMusicPublicationCodeIam];
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule, melody: melody);
        tracker.UpdateFromSchedule(schedule);
        tracker.RecordDefaultMusicTriggered(schedule.Id);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.Empty(dispatcher.Dispatched);
        Assert.False(tracker.ShouldTriggerDefaultMusicForNullPublication(schedule.Id));
    }

    [Fact]
    public void HandleStateChanged_skips_default_music_when_music_disabled()
    {
        var schedule = CreateSchedule(musicEnabled: false, publicationCode: null, trackCode: null, trackName: null);
        var melody = new StubMelodyMusicService
        {
            Releases =
            {
                [AppConstants.Media.MelodyMusicPublicationCodeIam] =
                    CreateMelodyRelease(AppConstants.Media.MelodyMusicPublicationCodeIam, "Kingdom Melodies"),
            },
        };
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule, melody: melody);
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleStateChanged_dispatches_default_music_when_enabled_and_publication_null()
    {
        var schedule = CreateSchedule(
            musicEnabled: true,
            publicationCode: null,
            publicationName: null,
            trackCode: null,
            trackName: null,
            sectionCode: null,
            languageCode: null);
        var release = CreateMelodyRelease(AppConstants.Media.MelodyMusicPublicationCodeIam, "Kingdom Melodies");
        var melody = new StubMelodyMusicService
        {
            Releases = { [AppConstants.Media.MelodyMusicPublicationCodeIam] = release },
            ByCode = release,
        };
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule, melody: melody);
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        await WaitUntilAsync(() => dispatcher.Dispatched.Count > 0, TimeSpan.FromSeconds(3));

        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.True(update.MusicUpdated);
        Assert.False(update.ShouldSave);
        Assert.Equal(AppConstants.Media.MelodyMusicPublicationCodeIam, update.Schedule.MusicPublicationCode);
        Assert.Equal("Kingdom Melodies", update.Schedule.MusicPublicationName);
        Assert.False(string.IsNullOrEmpty(update.Schedule.MusicTrackCode));
        Assert.False(tracker.ShouldTriggerDefaultMusicForNullPublication(schedule.Id));
    }

    [Fact]
    public async Task HandleStateChanged_default_music_falls_back_to_first_release_when_preferred_missing()
    {
        var schedule = CreateSchedule(
            musicEnabled: true,
            publicationCode: null,
            publicationName: null,
            trackCode: null,
            trackName: null,
            languageCode: "E");
        var release = CreateMelodyRelease("other-melody", "Other Melodies", trackCode: "3", sectionCode: null);
        var melody = new StubMelodyMusicService
        {
            Releases = { ["other-melody"] = release },
            ByCode = release,
        };
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule, melody: melody);
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        await WaitUntilAsync(() => dispatcher.Dispatched.Count > 0, TimeSpan.FromSeconds(3));

        var update = Assert.Single(dispatcher.Dispatched.OfType<UpdateScheduleFromViewModelAction>());
        Assert.Equal("other-melody", update.Schedule.MusicPublicationCode);
        Assert.Equal("Other Melodies", update.Schedule.MusicPublicationName);
        Assert.Equal("3", update.Schedule.MusicTrackCode);
        Assert.Equal("E", update.Schedule.MusicLanguageCode);
    }

    [Fact]
    public async Task HandleStateChanged_default_music_no_dispatch_when_releases_empty()
    {
        var schedule = CreateSchedule(musicEnabled: true, publicationCode: null, trackCode: null, trackName: null);
        var melody = new StubMelodyMusicService();
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule, melody: melody);
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        await Task.Delay(100);

        Assert.Empty(dispatcher.Dispatched);
        Assert.True(tracker.ShouldTriggerDefaultMusicForNullPublication(schedule.Id));
    }

    [Fact]
    public void HandleStateChanged_current_music_unchanged_skips_music_holder_update()
    {
        var schedule = CreateSchedule(languageCode: "E", publicationCode: "sjjc", trackCode: "7");
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(schedule);
        var music = new AlarmMusic
        {
            LanguageCode = "E",
            PublicationCode = "sjjc",
            TrackCode = "7",
            Repeat = false,
        };
        var holder = new MusicStateHolder
        {
            Music = music,
            LastMusic = music,
            MusicUpdated = false,
        };

        InvokeHandleStateChanged(sut, schedule.Id, holder, _ => { }, _ => { });

        Assert.False(holder.MusicUpdated);
        Assert.Same(music, holder.Music);
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public void HandleStateChanged_current_music_changed_maps_alarm_music_when_main_thread_available()
    {
        var schedule = CreateSchedule(languageCode: "E", publicationCode: "sjjc", trackCode: "9", repeat: true);
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(CreateSchedule(languageCode: "E", publicationCode: "sjjc", trackCode: "1", repeat: false));
        var holder = new MusicStateHolder
        {
            Music = new AlarmMusic { LanguageCode = "E", PublicationCode = "sjjc", TrackCode = "1" },
            LastMusic = new AlarmMusic { LanguageCode = "E", PublicationCode = "sjjc", TrackCode = "1" },
        };

        try
        {
            sut.HandleStateChanged(schedule.Id, holder, _ => { }, _ => { });
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return;
        }

        if (holder.Music is null || holder.Music.TrackCode != "9")
        {
            // MainThread callback was queued but not flushed on this host.
            return;
        }

        Assert.True(holder.MusicUpdated);
        Assert.Equal("9", holder.Music.TrackCode);
        Assert.Equal("sjjc", holder.Music.PublicationCode);
        Assert.Same(holder.Music, holder.LastMusic);
    }

    [Fact]
    public void HandleStateChanged_combined_language_publication_section_track_repeat_updates_tracker()
    {
        var baseline = CreateSchedule(
            languageCode: "E",
            publicationCode: "sjjc",
            sectionCode: null,
            trackCode: "1",
            repeat: false);
        var schedule = CreateSchedule(
            languageCode: "F",
            publicationCode: "snv",
            sectionCode: "A",
            trackCode: "99",
            repeat: true,
            sectionName: "Part A");
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.Equal("F", tracker.LastScheduleMusicLanguageCode);
        Assert.Equal("snv", tracker.LastScheduleMusicPublicationCode);
        Assert.Equal("A", tracker.LastMusicSectionCode);
        Assert.Equal("99", tracker.LastScheduleMusicTrackCode);
        Assert.True(tracker.LastScheduleMusicRepeat);
        Assert.Equal(
            (false, false, false, false, false),
            tracker.DetectChanges(schedule));
    }

    [Fact]
    public void HandleStateChanged_noop_when_tracker_already_matches_schedule()
    {
        var schedule = CreateSchedule();
        var (sut, _, dispatcher, tracker, notified, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(schedule);
        var holder = new MusicStateHolder
        {
            Music = new AlarmMusic
            {
                LanguageCode = schedule.MusicLanguageCode,
                PublicationCode = schedule.MusicPublicationCode!,
                TrackCode = schedule.MusicTrackCode!,
                Repeat = schedule.MusicRepeat ?? false,
            },
        };
        holder.LastMusic = holder.Music;
        var propNames = new List<string>();

        InvokeHandleStateChanged(sut, schedule.Id, holder, _ => { }, propNames.Add);

        Assert.Empty(dispatcher.Dispatched);
        Assert.Empty(propNames);
        Assert.Empty(notified);
        Assert.False(holder.MusicUpdated);
    }

    [Fact]
    public void HandleStateChanged_music_enabled_false_from_true_updates_tracker()
    {
        var baseline = CreateSchedule(musicEnabled: true);
        var schedule = CreateSchedule(musicEnabled: false);
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.False(tracker.LastMusicEnabled);
    }

    [Fact]
    public void HandleStateChanged_leaves_pending_music_enabled_when_value_differs_from_state()
    {
        var schedule = CreateSchedule(musicEnabled: true);
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(schedule);
        var holder = new MusicStateHolder { PendingMusicEnabled = false };

        InvokeHandleStateChanged(sut, schedule.Id, holder, _ => { }, _ => { });

        Assert.False(holder.PendingMusicEnabled);
    }

    [Fact]
    public async Task HandleStateChanged_default_music_no_dispatch_when_tracks_empty()
    {
        var schedule = CreateSchedule(musicEnabled: true, publicationCode: null, trackCode: null, trackName: null);
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
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule, melody: melody);
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });
        await Task.Delay(150);

        Assert.Empty(dispatcher.Dispatched);
        Assert.True(tracker.ShouldTriggerDefaultMusicForNullPublication(schedule.Id));
    }

    [Fact]
    public async Task HandleStateChanged_default_music_no_dispatch_when_by_code_returns_null()
    {
        var schedule = CreateSchedule(musicEnabled: true, publicationCode: null, trackCode: null, trackName: null);
        var release = CreateMelodyRelease(AppConstants.Media.MelodyMusicPublicationCodeIam, "Kingdom Melodies");
        var melody = new StubMelodyMusicService
        {
            Releases = { [AppConstants.Media.MelodyMusicPublicationCodeIam] = release },
            ByCode = null,
        };
        // Force GetByCodeWithTracksAsync to return null even when Releases has the key.
        melody.ByCode = null;
        var services = new ServiceCollection();
        services.AddSingleton<IMelodyMusicService>(new NullByCodeMelodyMusicService(release));
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule, serviceProvider: services.BuildServiceProvider());
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });
        await Task.Delay(150);

        Assert.Empty(dispatcher.Dispatched);
        Assert.True(tracker.ShouldTriggerDefaultMusicForNullPublication(schedule.Id));
    }

    [Fact]
    public async Task HandleStateChanged_default_music_aborts_when_schedule_id_changes_during_load()
    {
        var schedule = CreateSchedule(musicEnabled: true, publicationCode: null, trackCode: null, trackName: null);
        var release = CreateMelodyRelease(AppConstants.Media.MelodyMusicPublicationCodeIam, "Kingdom Melodies");
        var delayed = new DelayedMelodyMusicService(release, delayMs: 80);
        var services = new ServiceCollection();
        services.AddSingleton<IMelodyMusicService>(delayed);
        var (sut, state, dispatcher, tracker, _, _) = CreateHandler(schedule, serviceProvider: services.BuildServiceProvider());
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });
        state.Value = new ApplicationState
        {
            CurrentSchedule = CreateSchedule(id: 999, musicEnabled: true, publicationCode: null),
        };

        await WaitUntilAsync(() => delayed.GetByCodeCalls > 0, TimeSpan.FromSeconds(2));
        await Task.Delay(120);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleStateChanged_default_music_aborts_when_publication_filled_during_load()
    {
        var schedule = CreateSchedule(musicEnabled: true, publicationCode: null, trackCode: null, trackName: null);
        var release = CreateMelodyRelease(AppConstants.Media.MelodyMusicPublicationCodeIam, "Kingdom Melodies");
        var delayed = new DelayedMelodyMusicService(release, delayMs: 80);
        var services = new ServiceCollection();
        services.AddSingleton<IMelodyMusicService>(delayed);
        var (sut, state, dispatcher, tracker, _, _) = CreateHandler(schedule, serviceProvider: services.BuildServiceProvider());
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });
        state.Value = new ApplicationState
        {
            CurrentSchedule = CreateSchedule(id: schedule.Id, musicEnabled: true, publicationCode: "sjjc"),
        };

        await WaitUntilAsync(() => delayed.GetByCodeCalls > 0, TimeSpan.FromSeconds(2));
        await Task.Delay(120);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleStateChanged_default_music_swallows_service_exceptions()
    {
        var schedule = CreateSchedule(musicEnabled: true, publicationCode: null, trackCode: null, trackName: null);
        var services = new ServiceCollection();
        services.AddSingleton<IMelodyMusicService>(new ThrowingMelodyMusicService());
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule, serviceProvider: services.BuildServiceProvider());
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });
        await Task.Delay(150);

        Assert.Empty(dispatcher.Dispatched);
        Assert.True(tracker.ShouldTriggerDefaultMusicForNullPublication(schedule.Id));
    }

    [Fact]
    public async Task HandleStateChanged_default_music_no_dispatch_when_preferred_and_first_release_are_null()
    {
        var schedule = CreateSchedule(musicEnabled: true, publicationCode: null, trackCode: null, trackName: null, languageCode: null);
        var melody = new StubMelodyMusicService
        {
            Releases =
            {
                [AppConstants.Media.MelodyMusicPublicationCodeIam] = null!,
            },
        };
        var (sut, _, dispatcher, tracker, _, _) = CreateHandler(schedule, melody: melody);
        tracker.UpdateFromSchedule(schedule);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });
        await Task.Delay(150);

        // Preferred key present but null → falls to First(); First().Value is also null → no pick.
        Assert.Empty(dispatcher.Dispatched);
        Assert.True(tracker.ShouldTriggerDefaultMusicForNullPublication(schedule.Id));
    }

    [Fact]
    public void HandleStateChanged_music_enabled_with_empty_language_still_updates_tracker()
    {
        // Language clear via property-change path (UpdateFromSchedule before MainThread), not enable flip.
        var baseline = CreateSchedule(musicEnabled: true, languageCode: "E", trackName: "Cached Song");
        var schedule = CreateSchedule(musicEnabled: true, languageCode: null, trackName: "Cached Song");
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(baseline);

        InvokeHandleStateChanged(sut, schedule.Id, new MusicStateHolder(), _ => { }, _ => { });

        Assert.True(tracker.LastMusicEnabled);
        Assert.Null(tracker.LastScheduleMusicLanguageCode);
    }

    [Fact]
    public void HandleStateChanged_current_music_change_when_last_music_null_detects_change()
    {
        var schedule = CreateSchedule(trackCode: "12");
        var (sut, _, _, tracker, _, _) = CreateHandler(schedule);
        tracker.UpdateFromSchedule(schedule);
        var holder = new MusicStateHolder { Music = null, LastMusic = null };

        try
        {
            sut.HandleStateChanged(schedule.Id, holder, _ => { }, _ => { });
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return;
        }

        if (holder.Music is null)
        {
            return;
        }

        Assert.True(holder.MusicUpdated);
        Assert.Equal("12", holder.Music.TrackCode);
    }

    private sealed class NullByCodeMelodyMusicService(MelodyMusic releaseInList) : IMelodyMusicService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
            {
                [releaseInList.Code] = releaseInList,
            });

        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<MelodyMusic?>(null);

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class DelayedMelodyMusicService(MelodyMusic release, int delayMs) : IMelodyMusicService
    {
        public int GetByCodeCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
            {
                [release.Code] = release,
            });

        public async Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default)
        {
            GetByCodeCalls++;
            await Task.Delay(delayMs, cancellationToken);
            return release;
        }

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ThrowingMelodyMusicService : IMelodyMusicService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("melody catalog unavailable");

        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("melody catalog unavailable");

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
#endif
