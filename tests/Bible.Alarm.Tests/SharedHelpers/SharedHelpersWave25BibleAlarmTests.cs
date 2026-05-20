#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PubMediaLinksHostAttemptPlannerBibleAlarmTests
{
    [Fact]
    public void BuildThreeAttemptHostIndices_throws_when_host_count_not_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PubMediaLinksHostAttemptPlanner.BuildThreeAttemptHostIndices(0, _ => 0));
    }

    [Fact]
    public void BuildThreeAttemptHostIndices_single_host_always_zero()
    {
        var indices = PubMediaLinksHostAttemptPlanner.BuildThreeAttemptHostIndices(
            urlCount: 1,
            nextExclusiveBelowCount: _ => throw new InvalidOperationException("RNG must not run"));

        Assert.Equal([0, 0, 0], indices);
    }

    [Fact]
    public void BuildThreeAttemptHostIndices_second_attempt_wraps_with_modulo()
    {
        var indices = PubMediaLinksHostAttemptPlanner.BuildThreeAttemptHostIndices(
            urlCount: 5,
            nextExclusiveBelowCount: new RngQueue(4, 1).NextBelow);

        Assert.Equal([4, 0, 1], indices);
    }

    private sealed class RngQueue
    {
        private readonly Queue<int> queue;

        public RngQueue(params int[] sequence) => queue = new Queue<int>(sequence);

        public int NextBelow(int upperExclusive)
        {
            var next = queue.Dequeue();
            if (next >= upperExclusive)
            {
                throw new InvalidOperationException("RNG out of range.");
            }

            return next;
        }
    }
}

public sealed class MelodyMusicBibleAlarmTests
{
    [Fact]
    public void Forwarding_members_and_implicit_casts_round_trip()
    {
        var category = new Category { Id = 90, CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
        var language = new Language { Id = 1, LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var publication = new BiblePublication
        {
            Id = 7,
            Name = "Kingdom Melodies",
            PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            LanguageId = language.Id,
            Language = language,
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = true,
        };
        publication.BiblePublicationCategories.Add(new BiblePublicationCategory
        {
            BiblePublication = publication,
            Category = category,
            CategoryId = category.Id,
        });

        var sut = new MelodyMusic { Publication = publication };

        Assert.Equal(7, sut.Id);
        Assert.Equal(AppConstants.Media.MelodyMusicPublicationCodeIam, sut.Code);
        Assert.Equal(category.Id, sut.CategoryId);
        Assert.Same(category, sut.Category);
        Assert.Equal(language.Id, sut.LanguageId);
        Assert.Same(language, sut.Language);
        Assert.Same(publication.Sections, sut.Sections);
        Assert.Same(publication.Tracks, sut.Tracks);
        Assert.False(sut.IsVideo);

        MelodyMusic wrapped = publication;
        BiblePublication unwrapped = wrapped;
        Assert.Same(publication, unwrapped);
    }
}

public sealed class TagLibMimeConstantsBibleAlarmTests
{
    [Fact]
    public void Mime_constants_are_distinct_type_subtype_pairs()
    {
        Assert.Equal(2, TagLibMimeConstants.AudioMpeg.Split('/').Length);
        Assert.Equal(2, TagLibMimeConstants.VideoMp4.Split('/').Length);
        Assert.NotEqual(TagLibMimeConstants.AudioMpeg, TagLibMimeConstants.VideoMp4);
    }
}

public sealed class HomeViewModelDepsBibleAlarmTests
{
    private sealed class MutableState<T>(T value) : IState<T> where T : class
    {
        public T Value { get; set; } = value;
#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public void Dispatch(object action) { }
#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067
    }

    private sealed class UnusedNavigation : INavigationService
    {
        public void Dispose() { }
        public Task NavigateToHomeAsync(bool animated = true) => Task.CompletedTask;
        public Task NavigateToScheduleAsync() => Task.CompletedTask;
        public Task NavigateToScheduleAsync(int scheduleId, bool isEnabled) => Task.CompletedTask;
        public Task OpenSongPublicationSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenMusicTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenBibleSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenMusicSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenBiblePublicationTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenNumberOfTracksModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenLanguageModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenCategoryModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenPlaybackModalAsync(bool animated = false) => Task.CompletedTask;
        public Task OpenBatteryOptimizationModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenNotificationPermissionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task PopModalAsync() => Task.CompletedTask;
        public Task PopAsync() => Task.CompletedTask;
        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;
        public void PopAllModalsAndPages() { }
        public void ClearCache() { }
        public Views.Home? GetCurrentHomePage() => null;
        public Microsoft.Maui.Controls.Page? GetCurrentPage() => null;
        public bool IsPlaybackModalOnScreen() => false;
        public void SetMiniBarVisible(bool visible) { }
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<ScheduleMappingProfile>(), NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    [Fact]
    public void Record_stores_all_dependencies()
    {
        var logger = TestLogging.CreateLogger();
        var sp = new ServiceCollection().BuildServiceProvider();
        var app = new MutableState<ApplicationState>(new ApplicationState(new ObservableHashSet<ScheduleStateItem>()));
        var playback = new MutableState<PlaybackState>(new PlaybackState());
        var dispatcher = new RecordingDispatcher();
        var nav = new UnusedNavigation();
        var mapper = CreateMapper();

        var deps = new HomeViewModelDeps(logger, sp, app, playback, dispatcher, nav, mapper);

        Assert.Same(logger, deps.Logger);
        Assert.Same(sp, deps.ServiceProvider);
        Assert.Same(app, deps.ApplicationState);
        Assert.Same(playback, deps.PlaybackState);
        Assert.Same(dispatcher, deps.Dispatcher);
        Assert.Same(nav, deps.NavigationService);
        Assert.Same(mapper, deps.Mapper);
    }
}

public sealed class BiblePublicationTrackModelBibleAlarmTests
{
    private static BiblePublication Pub(int id = 42) =>
        new()
        {
            Id = id,
            Name = "Nw",
            PublicationCode = "nw",
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = false,
        };

    private static BiblePublicationTrack Track(BiblePublication pub, string code, int id = 0) =>
        new()
        {
            Id = id,
            TrackCode = code,
            Title = "T",
            BiblePublicationId = pub.Id,
            Publication = pub,
        };

    [Fact]
    public void CompareTo_orders_track_codes_and_operators()
    {
        var pub = Pub();
        var two = Track(pub, "2");
        var ten = Track(pub, "10");
        Assert.True(two.CompareTo(ten) < 0);
        Assert.True(two < ten);
        Assert.True(ten > two);
        Assert.Equal(0, Track(pub, "07").CompareTo(Track(pub, "7")));
        Assert.True(Track(pub, "07") <= Track(pub, "7"));
        Assert.True(Track(pub, "07") >= Track(pub, "7"));
        Assert.Equal(1, two.CompareTo((BiblePublicationTrack?)null));
        Assert.True(two.CompareTo(new object()) > 0);
        Assert.Equal(0, two.CompareTo((object)Track(pub, "2")));
    }

    [Fact]
    public void Equals_hash_and_null_operators_follow_model_rules()
    {
        var pub = Pub();
        var a = Track(pub, "1", id: 8);
        var b = Track(pub, "9", id: 8);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a.Equals((object?)null));
        Assert.False(a.Equals("x"));
        Assert.True(Track(pub, "1", id: 1) != Track(pub, "1", id: 2));
        var u = Track(pub, "3", id: 0);
        var v = Track(pub, "3", id: 0);
        Assert.False(u.Equals(v));
        Assert.NotEqual(u.GetHashCode(), v.GetHashCode());
        BiblePublicationTrack? n = null;
        Assert.False(u < n);
        Assert.False(n <= u);
        Assert.False(u >= n);
    }
}

public sealed class BiblePublicationSectionModelBibleAlarmTests
{
    private static BiblePublication Pub(int id = 42) =>
        new()
        {
            Id = id,
            Name = "B",
            PublicationCode = "nwt",
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = false,
        };

    private static BiblePublicationSection Section(BiblePublication pub, string code, int id = 0) =>
        new()
        {
            Id = id,
            Name = code,
            SectionCode = code,
            BiblePublicationId = pub.Id,
            BiblePublication = pub,
            Tracks = [],
        };

    [Fact]
    public void CompareTo_orders_section_codes_and_operators()
    {
        var pub = Pub();
        var two = Section(pub, "2");
        var ten = Section(pub, "10");
        Assert.True(two.CompareTo(ten) < 0);
        Assert.True(two < ten);
        Assert.Equal(0, Section(pub, "  10 ").CompareTo(Section(pub, "10")));
        Assert.Equal(0, Section(pub, "iam-a").CompareTo(Section(pub, "IAM-A")));
        Assert.True(Section(pub, "BETA") > Section(pub, "alpha"));
        Assert.Equal(1, two.CompareTo((BiblePublicationSection?)null));
        Assert.True(two.CompareTo(new object()) > 0);
        var boxed = Section(pub, "3");
        Assert.Equal(0, Section(pub, "3").CompareTo((object)boxed));
        Assert.True(Section(pub, "3") >= boxed && Section(pub, "3") <= boxed);
    }

    [Fact]
    public void Equals_hash_and_null_operators_follow_model_rules()
    {
        var pub = Pub();
        var a = Section(pub, "1", id: 5);
        var b = Section(pub, "9", id: 5);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a.Equals((object?)null));
        Assert.True(Section(pub, "1", id: 1) != Section(pub, "1", id: 2));
        var u = Section(pub, "3", id: 0);
        var v = Section(pub, "3", id: 0);
        Assert.False(u.Equals(v));
        Assert.NotEqual(u.GetHashCode(), v.GetHashCode());
        BiblePublicationSection? n = null;
        Assert.False(u < n);
        Assert.False(n <= u);
        Assert.False(u >= n);
    }
}

public sealed class BiblePublicationScheduleModelBibleAlarmTests
{
    [Fact]
    public void Model_properties_round_trip()
    {
        var schedule = new AlarmSchedule
        {
            Id = 4,
            Name = "Bible",
            IsEnabled = true,
            Hour = 6,
            Minute = 30,
            Second = 0,
            DaysOfWeek = WeekDays.Friday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };
        var model = new BiblePublicationSchedule
        {
            AlarmScheduleId = schedule.Id,
            AlarmSchedule = schedule,
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = "1",
            FinishedDuration = TimeSpan.FromMinutes(3),
        };

        Assert.Equal(4, model.AlarmScheduleId);
        Assert.Same(schedule, model.AlarmSchedule);
        Assert.Equal("nwt", model.PublicationCode);
        Assert.Equal(TimeSpan.FromMinutes(3), model.FinishedDuration);
    }
}

public sealed class AlarmMusicModelBibleAlarmTests
{
    [Fact]
    public void Model_properties_round_trip()
    {
        var schedule = new AlarmSchedule
        {
            Id = 3,
            Name = "Music alarm",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
        };
        var model = new AlarmMusic
        {
            Id = 11,
            PublicationCode = "osg",
            LanguageCode = "E",
            SectionCode = "1",
            TrackCode = "2",
            Repeat = true,
            AlarmScheduleId = schedule.Id,
            AlarmSchedule = schedule,
        };

        Assert.Equal(11, model.Id);
        Assert.Equal("osg", model.PublicationCode);
        Assert.Equal("E", model.LanguageCode);
        Assert.Equal("1", model.SectionCode);
        Assert.Equal("2", model.TrackCode);
        Assert.True(model.Repeat);
        Assert.Same(schedule, model.AlarmSchedule);
    }
}
