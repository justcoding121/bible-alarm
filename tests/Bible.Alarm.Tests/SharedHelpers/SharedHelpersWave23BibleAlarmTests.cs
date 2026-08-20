#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Tests;

public sealed class TrackUrlModelBibleAlarmTests
{
    [Fact]
    public void Model_properties_round_trip()
    {
        var track = new BiblePublicationTrack { Id = 3, TrackCode = "1" };
        var model = new TrackUrl
        {
            Id = 1,
            Url = "https://cdn.example/track.mp3",
            BiblePublicationTrackId = track.Id,
            BiblePublicationTrack = track,
        };

        Assert.Equal(1, model.Id);
        Assert.Equal("https://cdn.example/track.mp3", model.Url);
        Assert.Equal(3, model.BiblePublicationTrackId);
        Assert.Same(track, model.BiblePublicationTrack);
    }
}

public sealed class BiblePublicationModelBibleAlarmTests
{
    [Fact]
    public void PrimaryCategory_properties_empty_when_no_categories()
    {
        var model = new BiblePublication { BiblePublicationCategories = [] };

        Assert.Null(model.PrimaryCategory);
        Assert.Equal(0, model.PrimaryCategoryId);
    }

    [Fact]
    public void PrimaryCategory_properties_use_first_category()
    {
        var category = new Category { Id = 8, CategoryCode = "Music" };
        var model = new BiblePublication
        {
            BiblePublicationCategories =
            [
                new BiblePublicationCategory { CategoryId = category.Id, Category = category },
            ],
            CatalogType = CatalogType.Flat,
        };

        Assert.Same(category, model.PrimaryCategory);
        Assert.Equal(8, model.PrimaryCategoryId);
        Assert.Equal(CatalogType.Flat, model.CatalogType);
    }
}

public sealed class TrackMetadataBibleAlarmTests
{
    [Fact]
    public void LookUpPath_getter_throws_when_unset()
    {
        var meta = new TrackMetadata();

        Assert.Throws<InvalidOperationException>(() => _ = meta.LookUpPath);
    }

    [Fact]
    public void Optional_properties_and_alarm_music_flag()
    {
        var when = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var meta = new TrackMetadata
        {
            ScheduleId = 4,
            NotificationTime = when,
            NaturalKey = "nk-1",
            LookUpPath = "/path",
            IsBibleContent = false,
        };

        Assert.Equal(when, meta.NotificationTime);
        Assert.Equal("nk-1", meta.NaturalKey);
        Assert.True(meta.IsAlarmMusic);
        Assert.Equal(PlayType.Music, meta.PlayType);
        Assert.True(meta.TryGetLookUpPath(out var path));
        Assert.Equal("/path", path);
    }
}

public sealed class ToastPopupOffsetsBibleAlarmTests
{
    [Fact]
    public void HorizontalCenterOffset_centers_inner_width()
    {
        Assert.Equal(25, ToastPopupOffsets.HorizontalCenterOffset(100, 50));
    }

    [Fact]
    public void VerticalOffsetAboveBottom_subtracts_inner_height_and_margin()
    {
        Assert.Equal(40, ToastPopupOffsets.VerticalOffsetAboveBottom(120, 30, 50));
    }
}

public sealed class ToastPopupThemeArgbBibleAlarmTests
{
    [Fact]
    public void Background_returns_dark_or_light_tuple()
    {
        Assert.Equal(((byte)0xF2, (byte)0x2A, (byte)0x2A, (byte)0x2A), ToastPopupThemeArgb.Background(isDarkTheme: true));
        Assert.Equal(((byte)0xCC, (byte)0, (byte)0, (byte)0), ToastPopupThemeArgb.Background(isDarkTheme: false));
    }
}

public sealed class LanguageNameByLanguageModelBibleAlarmTests
{
    [Fact]
    public void Model_properties_round_trip()
    {
        var language = new Language { Id = 2, LanguageCode = "E" };
        var model = new LanguageNameByLanguage
        {
            Id = 1,
            LanguageId = language.Id,
            Language = language,
            DisplayLanguageCode = "E",
            Name = "English",
        };

        Assert.Equal(1, model.Id);
        Assert.Equal(2, model.LanguageId);
        Assert.Same(language, model.Language);
        Assert.Equal("E", model.DisplayLanguageCode);
        Assert.Equal("English", model.Name);
    }
}

public sealed class ScheduledToastNotificationIdMatcherBibleAlarmTests
{
    [Fact]
    public void MatchesSchedule_accepts_legacy_and_suffixed_ids()
    {
        Assert.True(ScheduledToastNotificationIdMatcher.MatchesSchedule(42, "42"));
        Assert.True(ScheduledToastNotificationIdMatcher.MatchesSchedule(42, "42_abc123"));
        Assert.False(ScheduledToastNotificationIdMatcher.MatchesSchedule(42, "420"));
        Assert.False(ScheduledToastNotificationIdMatcher.MatchesSchedule(42, "4_42"));
    }

    [Fact]
    public void MatchesSchedule_throws_when_toast_id_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ScheduledToastNotificationIdMatcher.MatchesSchedule(1, null!));
    }
}

public sealed class JwMediatorVideoCategoryApiRelativePathBibleAlarmTests
{
    [Fact]
    public void Compose_builds_mediator_categories_path()
    {
        var path = JwMediatorVideoCategoryApiRelativePath.Compose("E", "VideoOnDemand");
        Assert.Equal($"{AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix}/E/VideoOnDemand", path);
    }

    [Fact]
    public void Compose_throws_for_blank_inputs()
    {
        Assert.Throws<ArgumentException>(() => JwMediatorVideoCategoryApiRelativePath.Compose(" ", "x"));
        Assert.Throws<ArgumentException>(() => JwMediatorVideoCategoryApiRelativePath.Compose("E", ""));
    }
}
