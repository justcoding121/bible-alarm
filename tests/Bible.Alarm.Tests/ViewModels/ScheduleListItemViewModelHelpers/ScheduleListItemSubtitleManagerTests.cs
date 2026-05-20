#nullable enable

using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class ScheduleListItemSubtitleManagerTests
{
    private sealed class MutableState<T> : IState<T>
        where T : class
    {
        public MutableState(T value) => Value = value;

        public T Value { get; set; }

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class ThrowingApplicationState : IState<ApplicationState>
    {
        public ApplicationState Value =>
            throw new InvalidOperationException("application state unavailable");

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private static MutableState<PlaybackState> Playback(int scheduleId, bool preparingOrPlaying, string? title) =>
        new(new PlaybackState
        {
            CurrentScheduleId = scheduleId,
            IsPreparingOrPlaying = preparingOrPlaying,
            Title = title,
            Status = PlayStatus.Playing,
        });

    [Fact]
    public void RefreshSubTitleFromState_invalid_schedule_id_no_updates()
    {
        var schedules = new ObservableHashSet<ScheduleStateItem>();
        var app = new MutableState<ApplicationState>(new ApplicationState(schedules));
        var playback = Playback(1, true, "x");
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var subs = new List<string>();
        var langs = new List<string>();
        var props = new List<string>();

        sut.RefreshSubTitleFromState(
            0,
            null,
            s => subs.Add(s),
            langs.Add,
            props.Add);

        Assert.Empty(subs);
        Assert.Empty(langs);
        Assert.Empty(props);
    }

    [Fact]
    public void RefreshSubTitleFromState_negative_schedule_id_no_updates()
    {
        var schedules = new ObservableHashSet<ScheduleStateItem>();
        var app = new MutableState<ApplicationState>(new ApplicationState(schedules));
        var playback = Playback(1, true, "x");
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var subs = new List<string>();
        var langs = new List<string>();
        var props = new List<string>();

        sut.RefreshSubTitleFromState(
            -3,
            null,
            s => subs.Add(s),
            langs.Add,
            props.Add);

        Assert.Empty(subs);
        Assert.Empty(langs);
        Assert.Empty(props);
    }

    [Fact]
    public void RefreshSubTitleFromState_swallows_application_state_access_errors()
    {
        var app = new ThrowingApplicationState();
        var playback = Playback(99, false, null);
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var subs = new List<string>();
        var langs = new List<string>();
        var props = new List<string>();

        var ex = Record.Exception(() => sut.RefreshSubTitleFromState(
            99,
            null,
            subs.Add,
            langs.Add,
            props.Add));

        Assert.Null(ex);
        Assert.Empty(subs);
    }

    [Fact]
    public void RefreshSubTitleFromState_waiting_for_flat_catalog_track_title_leaves_subtitle_empty()
    {
        var item = new ScheduleStateItem
        {
            Id = 8,
            BiblePublicationScheduleId = 80,
            BiblePublicationCode = "dramas",
            BiblePublicationTrackTitle = "",
        };
        var app = new MutableState<ApplicationState>(new ApplicationState([]));
        var playback = Playback(8, false, null);
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var subtitles = new List<string>();

        sut.RefreshSubTitleFromState(8, item, subtitles.Add, _ => { }, _ => { });

        Assert.Empty(subtitles);
    }

    [Fact]
    public void RefreshSubTitleFromState_applies_language_name_when_present()
    {
        var item = new ScheduleStateItem
        {
            Id = 12,
            BiblePublicationScheduleId = 120,
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationCategoryName = "Bible",
            BiblePublicationName = "NWT",
            BiblePublicationSectionName = "Gen",
            BiblePublicationTrackCode = "1",
            BiblePublicationLanguageName = "  French  ",
        };
        var app = new MutableState<ApplicationState>(new ApplicationState([]));
        var playback = Playback(12, false, null);
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var langs = new List<string>();

        sut.RefreshSubTitleFromState(12, item, _ => { }, langs.Add, _ => { });

        Assert.Equal("French", Assert.Single(langs));
    }

    [Fact]
    public void RefreshSubTitleFromState_without_bible_publication_schedule_clears_language()
    {
        var item = new ScheduleStateItem { Id = 3, BiblePublicationScheduleId = null };
        var schedules = new ObservableHashSet<ScheduleStateItem> { item };
        var app = new MutableState<ApplicationState>(new ApplicationState(schedules));
        var playback = Playback(3, true, "live");
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var langs = new List<string>();
        var props = new List<string>();

        sut.RefreshSubTitleFromState(3, item, _ => { }, langs.Add, props.Add);

        Assert.Single(langs);
        Assert.Equal(string.Empty, langs[0]);
        Assert.Contains("Language", props);
        Assert.DoesNotContain("SubTitle", props);
    }

    [Fact]
    public void RefreshSubTitleFromState_resolves_item_from_application_state_when_not_provided()
    {
        var item = new ScheduleStateItem
        {
            Id = 7,
            BiblePublicationScheduleId = 70,
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationCategoryName = "Cat",
            BiblePublicationName = "Pub",
            BiblePublicationSectionName = "Matthew",
            BiblePublicationTrackCode = "40",
            BiblePublicationLanguageName = "English",
        };
        var schedules = new ObservableHashSet<ScheduleStateItem> { item };
        var app = new MutableState<ApplicationState>(new ApplicationState(schedules));
        var playback = Playback(7, true, "Playing title");
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var subtitles = new List<string>();

        sut.RefreshSubTitleFromState(7, null, subtitles.Add, _ => { }, _ => { });

        Assert.Single(subtitles);
        Assert.Contains("Matthew", subtitles[0], StringComparison.Ordinal);
        // NWT is not a flat music publication: live playback title does not replace subtitle parts.
        Assert.Contains("40", subtitles[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Playing title", subtitles[0], StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshSubTitleFromState_waiting_for_section_display_names_leaves_subtitle_unchanged()
    {
        var item = new ScheduleStateItem
        {
            Id = 2,
            BiblePublicationScheduleId = 20,
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationSectionName = "",
            BiblePublicationTrackTitle = "",
            BiblePublicationTrackCode = null,
        };
        var app = new MutableState<ApplicationState>(new ApplicationState([]));
        var playback = Playback(2, false, null);
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var subtitles = new List<string>();

        sut.RefreshSubTitleFromState(2, item, subtitles.Add, _ => { }, _ => { });

        Assert.Empty(subtitles);
    }

    [Fact]
    public void RefreshSubTitleFromState_empty_subtitle_without_waiting_leaves_subtitle_unchanged()
    {
        var item = new ScheduleStateItem
        {
            Id = 17,
            BiblePublicationScheduleId = 170,
            BiblePublicationCode = AppConstants.Media.MediatorCategoryKeyChildrenSongs,
            BiblePublicationCategoryName = "   ",
            BiblePublicationName = "  ",
            BiblePublicationTrackTitle = "Episode",
            BiblePublicationLanguageName = "  ",
        };
        var app = new MutableState<ApplicationState>(new ApplicationState([]));
        var playback = Playback(17, false, null);
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var subtitles = new List<string>();

        sut.RefreshSubTitleFromState(17, item, subtitles.Add, _ => { }, _ => { });

        Assert.Single(subtitles);
        Assert.Contains("Episode", subtitles[0], StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshSubTitleFromState_flat_publication_uses_track_title_in_subtitle()
    {
        var item = new ScheduleStateItem
        {
            Id = 16,
            BiblePublicationScheduleId = 160,
            BiblePublicationCode = AppConstants.Media.MediatorCategoryKeyChildrenSongs,
            BiblePublicationCategoryName = "Children",
            BiblePublicationName = "Songs",
            BiblePublicationTrackTitle = "Song One",
        };
        var app = new MutableState<ApplicationState>(new ApplicationState([]));
        var playback = Playback(16, false, null);
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var subtitles = new List<string>();

        sut.RefreshSubTitleFromState(16, item, subtitles.Add, _ => { }, _ => { });

        Assert.Single(subtitles);
        Assert.Contains("Song One", subtitles[0], StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshSubTitleFromState_sectioned_music_uses_playback_title_when_same_schedule_active()
    {
        var item = new ScheduleStateItem
        {
            Id = 11,
            BiblePublicationScheduleId = 110,
            BiblePublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryMusic,
            BiblePublicationName = "KM",
            BiblePublicationSectionName = "Disc 1",
            BiblePublicationTrackTitle = "Stale",
        };
        var app = new MutableState<ApplicationState>(new ApplicationState([]));
        var playback = Playback(11, true, "Live track");
        var sut = new ScheduleListItemSubtitleManager(TestLogging.CreateLogger(), app, playback);

        var subtitles = new List<string>();

        sut.RefreshSubTitleFromState(11, item, subtitles.Add, _ => { }, _ => { });

        Assert.Single(subtitles);
        Assert.Contains("Live track", subtitles[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Stale", subtitles[0], StringComparison.Ordinal);
    }
}
