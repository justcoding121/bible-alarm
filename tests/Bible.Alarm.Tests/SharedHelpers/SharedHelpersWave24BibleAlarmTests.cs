#nullable enable

using System.Collections;
using System.IO;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Tests;

public sealed class ToastMessageNormalizerBibleAlarmTests
{
    [Fact]
    public void Normalize_returns_empty_for_null_whitespace_or_trims_content()
    {
        Assert.Equal(string.Empty, ToastMessageNormalizer.Normalize(null!));
        Assert.Equal(string.Empty, ToastMessageNormalizer.Normalize("   "));
        Assert.Equal("hello", ToastMessageNormalizer.Normalize("  hello  "));
    }
}

public sealed class MediaUriSchemeConstantsBibleAlarmTests
{
    [Fact]
    public void FileUriTripleSlashPrefix_composes_from_file_prefix_and_alt_separator()
    {
        Assert.Equal(
            MediaUriSchemeConstants.FilePrefix + Path.AltDirectorySeparatorChar,
            MediaUriSchemeConstants.FileUriTripleSlashPrefix);
    }
}

public sealed class FontBodyPointsDensityScalerBibleAlarmTests
{
    [Fact]
    public void ScalePointsWithDensityClamp_clamps_and_validates_multiplier()
    {
        Assert.Equal(18.0, FontBodyPointsDensityScaler.ScalePointsWithDensityClamp(18.0, density: 0, maxDensityMultiplier: 2.0));
        Assert.Equal(20.0, FontBodyPointsDensityScaler.ScalePointsWithDensityClamp(10.0, density: 10.0, maxDensityMultiplier: 2.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FontBodyPointsDensityScaler.ScalePointsWithDensityClamp(10.0, density: 2.0, maxDensityMultiplier: 0));
    }
}

public sealed class CollectionViewValidityPrecheckBibleAlarmTests
{
    [Fact]
    public void HasRenderableItemsSourceWithHandler_requires_source_handler_and_item()
    {
        var marker = new object();
        Assert.False(CollectionViewValidityPrecheck.HasRenderableItemsSourceWithHandler(null, true, marker));
        Assert.False(CollectionViewValidityPrecheck.HasRenderableItemsSourceWithHandler(new List<object>(), true, marker));
        Assert.False(CollectionViewValidityPrecheck.HasRenderableItemsSourceWithHandler(new List<object> { marker }, false, marker));
        Assert.True(CollectionViewValidityPrecheck.HasRenderableItemsSourceWithHandler(new List<object> { marker }, true, marker));
    }
}

public sealed class PlannerIterationClampBibleAlarmTests
{
    [Fact]
    public void Normalize_clamps_to_inclusive_bounds()
    {
        Assert.Equal(5, PlannerIterationClamp.Normalize(1, 5, 10));
        Assert.Equal(10, PlannerIterationClamp.Normalize(99, 5, 10));
        Assert.Equal(7, PlannerIterationClamp.Normalize(7, 5, 10));
    }
}

public sealed class PublicationLanguageFetchLookupNormalizerBibleAlarmTests
{
    [Fact]
    public void NormalizeLanguageCode_trims_and_uppercases()
    {
        Assert.Equal("EN", PublicationLanguageFetchLookupNormalizer.NormalizeLanguageCodeForPublicationLanguageJoin("  en "));
        Assert.Throws<ArgumentException>(() =>
            PublicationLanguageFetchLookupNormalizer.NormalizeLanguageCodeForPublicationLanguageJoin("   "));
    }

    [Fact]
    public void ResolvePublicationCode_uses_canonical_or_original_code()
    {
        Assert.Equal(
            AppConstants.Media.BiblePublicationCodeDramasGoodNews,
            PublicationLanguageFetchLookupNormalizer.ResolvePublicationCodeForDatabaseLookup(
                AppConstants.Media.NormalizedPublicationCodeDramasGoodNews));
        Assert.Equal("custom-pub", PublicationLanguageFetchLookupNormalizer.ResolvePublicationCodeForDatabaseLookup("custom-pub"));
    }
}

public sealed class VocalMusicBibleAlarmTests
{
    [Fact]
    public void Forwarding_members_and_implicit_casts_round_trip()
    {
        var category = new Category { Id = 5, CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
        var language = new Language { Id = 3, LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var publication = new BiblePublication
        {
            Id = 9,
            Name = "Songbook",
            PublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
            LanguageId = language.Id,
            Language = language,
            Sections = [],
            Tracks = [],
            IsVideo = true,
            IsMusic = true,
        };
        publication.BiblePublicationCategories.Add(new BiblePublicationCategory
        {
            BiblePublication = publication,
            Category = category,
            CategoryId = category.Id,
        });

        var sut = new VocalMusic { Publication = publication };

        Assert.Equal(9, sut.Id);
        Assert.Equal(publication.PublicationCode, sut.Code);
        Assert.Equal(publication.Name, sut.Name);
        Assert.Equal(category.Id, sut.CategoryId);
        Assert.Same(category, sut.Category);
        Assert.Equal(language.Id, sut.LanguageId);
        Assert.Same(language, sut.Language);
        Assert.Same(publication.Sections, sut.Sections);
        Assert.Same(publication.Tracks, sut.Tracks);
        Assert.True(sut.IsVideo);

        VocalMusic wrapped = publication;
        BiblePublication unwrapped = wrapped;
        Assert.Same(publication, wrapped.Publication);
        Assert.Same(publication, unwrapped);
    }
}

public sealed class PlayItemBibleAlarmTests
{
    [Fact]
    public void ToString_formats_music_and_bible_metadata()
    {
        var music = new PlayItem(new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "osg",
            IsBibleContent = false,
            TrackCode = "3",
        }, "https://x");
        Assert.Equal("E osg 3", music.ToString());

        var bible = new PlayItem(new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = "nwt",
            IsBibleContent = true,
            SectionCode = "40",
            TrackCode = "12",
        }, "https://y");
        Assert.Equal("E nwt 40 12", bible.ToString());
    }

    [Fact]
    public void Cdn_recovery_flags_can_be_toggled()
    {
        var sut = new PlayItem(new TrackMetadata { TrackCode = "1" }, "u");
        sut.CdnStaleUrlRecoveryConsumed = true;
        sut.CdnStaleUrlRefetchReplayIssued = true;
        sut.StreamingOpenPhaseMediaFailedRetryDone = true;
        Assert.True(sut.CdnStaleUrlRecoveryConsumed);
        Assert.True(sut.CdnStaleUrlRefetchReplayIssued);
        Assert.True(sut.StreamingOpenPhaseMediaFailedRetryDone);
    }
}

public sealed class AlarmNotificationBibleAlarmTests
{
    [Fact]
    public void Properties_round_trip_on_entity()
    {
        var schedule = new AlarmSchedule
        {
            Id = 7,
            Name = "Morning",
            IsEnabled = true,
            Hour = 8,
            Minute = 9,
            Second = 10,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };
        var when = DateTimeOffset.Parse("2027-06-01T08:00:00Z");
        var sut = new AlarmNotification
        {
            Id = 100,
            ScheduledTime = when,
            Sent = false,
            Fired = false,
            AlarmScheduleId = schedule.Id,
            AlarmSchedule = schedule,
            CancellationRequested = true,
            Cancelled = false,
        };

        Assert.Equal(100L, sut.Id);
        Assert.Equal(when, sut.ScheduledTime);
        Assert.Equal(7, sut.AlarmScheduleId);
        Assert.Same(schedule, sut.AlarmSchedule);
        Assert.True(sut.CancellationRequested);
        sut.Sent = true;
        Assert.True(sut.Sent);
    }
}

public sealed class ItemsSourcePresenceBibleAlarmTests
{
    private sealed class ThrowingEnumerable : IEnumerable
    {
        public IEnumerator GetEnumerator() => throw new InvalidOperationException();
    }

    private sealed class WeirdCollection : ICollection
    {
        public int Count => 3;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public void CopyTo(Array array, int index) => throw new NotSupportedException();
        public IEnumerator GetEnumerator() { yield break; }
    }

    private sealed class EnumerableOnly : IEnumerable
    {
        private readonly object[] items;

        public EnumerableOnly(params object[] items) => this.items = items;

        public IEnumerator GetEnumerator()
        {
            foreach (var item in items)
            {
                yield return item;
            }
        }
    }

    [Fact]
    public void HasAnyItems_handles_null_empty_throw_and_collection_count()
    {
        Assert.False(ItemsSourcePresence.HasAnyItems(null));
        Assert.False(ItemsSourcePresence.HasAnyItems(new List<object>()));
        Assert.False(ItemsSourcePresence.HasAnyItems(new ThrowingEnumerable()));
        Assert.False(ItemsSourcePresence.HasAnyItems(42));
        Assert.True(ItemsSourcePresence.HasAnyItems(new WeirdCollection()));
        Assert.True(ItemsSourcePresence.HasAnyItems(new List<object> { 1 }));
        Assert.True(ItemsSourcePresence.HasAnyItems(new EnumerableOnly(1)));
        Assert.False(ItemsSourcePresence.HasAnyItems(new EnumerableOnly()));
    }

    [Fact]
    public void ContainsItem_matches_by_reference_equality_or_value()
    {
        Assert.False(ItemsSourcePresence.ContainsItem(null, new object()));
        Assert.False(ItemsSourcePresence.ContainsItem(new List<object>(), null));
        Assert.False(ItemsSourcePresence.ContainsItem(new ThrowingEnumerable(), new object()));
        Assert.False(ItemsSourcePresence.ContainsItem(42, 1));
        Assert.False(ItemsSourcePresence.ContainsItem(new List<object> { "a" }, "b"));

        object src = new List<string> { "alpha" };
        Assert.True(ItemsSourcePresence.ContainsItem(src, new string("alpha".ToCharArray())));

        var marker = new object();
        Assert.True(ItemsSourcePresence.ContainsItem(new List<object> { marker }, marker));
        Assert.True(ItemsSourcePresence.ContainsItem(new EnumerableOnly(marker), marker));
    }
}
