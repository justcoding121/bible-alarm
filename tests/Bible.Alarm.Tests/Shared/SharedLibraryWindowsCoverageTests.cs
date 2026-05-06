#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Tests;

/// <summary>
/// Exercises <c>Bible.Alarm.Shared</c> from this test project so OpenCover/Sonar attributes coverage
/// when only <c>Bible.Alarm.Tests</c> is run on Windows (the shared test assembly is not referenced here).
/// </summary>
public sealed class SharedLibraryWindowsCoverageTests
{
    [Fact]
    public void ObservableHashSet_maintains_sorted_order_and_non_generic_copy()
    {
        var set = new ObservableHashSet<int>();
        set.Add(3);
        set.Add(1);
        Assert.Equal([1, 3], set.ToArray());

        System.Collections.ICollection raw = set;
        var boxed = new object?[2];
        raw.CopyTo(boxed, 0);
        Assert.Equal(1, boxed[0]);
        Assert.Equal(3, boxed[1]);
    }

    [Fact]
    public async Task AsyncQueue_fifo_buffer_then_dequeue()
    {
        using var q = new AsyncQueue<int>();
        await q.EnqueueAsync(10);
        await q.EnqueueAsync(20);
        Assert.Equal(10, await q.DequeueAsync());
        Assert.Equal(20, await q.DequeueAsync());
    }

    [Fact]
    public void PublicationCodeHelper_normalize_and_priority_delegation()
    {
        Assert.Null(PublicationCodeHelper.Normalize("  "));
        Assert.Equal("nwt", PublicationCodeHelper.Normalize("  nwt "));
        Assert.Equal(0, PublicationCodeHelper.GetPublicationSortPriority(AppConstants.Media.BiblePublicationCodeNwt));
        Assert.True(PublicationCodeHelper.CodeEquals("01", "1"));
    }

    [Fact]
    public void PublicationSortHelper_orders_priority_codes_before_others()
    {
        var rows = new[]
        {
            new { Code = "zzz", Name = "Z" },
            new { Code = AppConstants.Media.BiblePublicationCodeNwt, Name = "A" },
        };
        var ordered = PublicationSortHelper
            .SortByPriority(rows, static r => r.Code, static r => r.Name)
            .ToList();
        Assert.Equal(AppConstants.Media.BiblePublicationCodeNwt, ordered[0].Code);
    }

    [Fact]
    public void PublicationTypeHelper_detects_drama_and_catalog_defaults()
    {
        Assert.True(PublicationTypeHelper.IsDrama(AppConstants.Media.BiblePublicationCategoryDramas));
        Assert.Equal(CatalogType.Sectioned, PublicationTypeHelper.GetCatalogType(null));
        Assert.False(PublicationTypeHelper.IsVideo(AppConstants.Media.BiblePublicationCodeDramaticBibleReadings));
    }

    [Fact]
    public void Publication_compare_equals_operators_and_non_publication_compare()
    {
        var alpha = new Publication { Name = "Alpha", PublicationCode = "a" };
        var beta = new Publication { Name = "Beta", PublicationCode = "b" };
        Assert.True(alpha < beta);
        Assert.True(beta > alpha);
        Assert.Equal(1, alpha.CompareTo("x"));

        var alphaAgain = new Publication { Name = "Alpha", PublicationCode = "z" };
        Assert.True(alpha.Equals(alphaAgain));
        Assert.True(alpha == alphaAgain);
        Assert.False(alpha != alphaAgain);
    }

    [Fact]
    public void TranslatedPublication_distinguishes_language_in_equality()
    {
        var a = new TranslatedPublication { Name = "N", PublicationCode = "c", LanguageId = 1 };
        var b = new TranslatedPublication { Name = "N", PublicationCode = "c", LanguageId = 2 };
        Assert.NotEqual(a, b);
        Assert.Equal(a, new TranslatedPublication { Name = "N", PublicationCode = "c", LanguageId = 1 });
    }

    [Fact]
    public void AlarmSchedule_meridian_meridian_hour_time_text_and_ordering()
    {
        var midnight = new AlarmSchedule { Hour = 0, Minute = 5, DaysOfWeek = WeekDays.Monday };
        Assert.Equal(Meridian.Am, midnight.Meridian);
        Assert.Equal(12, midnight.MeridianHour);
        Assert.Equal("12:05", midnight.TimeText);

        var noon = new AlarmSchedule { Hour = 12, Minute = 0, DaysOfWeek = WeekDays.Monday };
        Assert.Equal(Meridian.Pm, noon.Meridian);
        Assert.Equal(12, noon.MeridianHour);

        var afternoon = new AlarmSchedule { Hour = 15, Minute = 7, DaysOfWeek = WeekDays.Tuesday };
        Assert.Equal(Meridian.Pm, afternoon.Meridian);
        Assert.Equal(3, afternoon.MeridianHour);
        Assert.Equal("03:07", afternoon.TimeText);

        var first = new AlarmSchedule { Id = 1 };
        var second = new AlarmSchedule { Id = 2 };
        Assert.True(first < second);
        Assert.True(second > first);
        Assert.True(first.Equals(new AlarmSchedule { Id = 1 }));
    }

    [Fact]
    public void AudioDescriptionTitlePhrases_matches_embedded_english_phrase()
    {
        Assert.True(AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase(
            AppConstants.Media.DefaultLanguageCode,
            "With Audio Descriptions"));
        Assert.NotEmpty(AudioDescriptionTitlePhrases.GetPhrasesForLanguage(AppConstants.Media.DefaultLanguageCode));
    }

    [Fact]
    public void MusicTrack_orders_by_track_code_and_handles_null_code_hash()
    {
        var two = new MusicTrack { TrackCode = "2", Title = "b" };
        var ten = new MusicTrack { TrackCode = "10", Title = "a" };
        Assert.True(two < ten);
        Assert.True(ten > two);
        Assert.Equal(1, two.CompareTo("x"));

        var dupLeft = new MusicTrack { TrackCode = "01", Title = "a" };
        var dupRight = new MusicTrack { TrackCode = "1", Title = "b" };
        Assert.Equal(0, dupLeft.CompareTo(dupRight));
        Assert.True(dupLeft.Equals(dupRight));

        var nullCode = new MusicTrack { TrackCode = null, Title = "z" };
        Assert.Equal(1, nullCode.CompareTo(null));
    }

    [Fact]
    public void CodeComparisonHelper_orders_numeric_and_string_and_nulls()
    {
        Assert.Equal(0, CodeComparisonHelper.Compare(null, null));
        Assert.True(CodeComparisonHelper.Compare(null, "a") < 0);
        Assert.True(CodeComparisonHelper.Compare("b", null) > 0);
        Assert.True(CodeComparisonHelper.Compare("2", "10") < 0);
        Assert.True(CodeComparisonHelper.Compare("b", "a") > 0);

        Assert.True(CodeComparisonHelper.Equals("01", "1"));
        Assert.False(CodeComparisonHelper.Equals(null, "x"));
        Assert.True(CodeComparisonHelper.Equals(null, null));
    }

    [Fact]
    public void PublicationTypeHelper_track_label_and_catalog_types()
    {
        Assert.Equal(AppConstants.Media.PublicationUiTrackSingular,
            PublicationTypeHelper.GetTrackLabel(AppConstants.Media.BiblePublicationCodeNwt));
        Assert.Equal(AppConstants.Media.PublicationUiPartSingular,
            PublicationTypeHelper.GetTrackLabel(AppConstants.Media.MusicPublicationCodeOsg));

        Assert.Equal(CatalogType.IssueSectioned,
            PublicationTypeHelper.GetCatalogType($"w{MagazineHelper.MagazineStartYear}"));
        Assert.Equal(CatalogType.Flat,
            PublicationTypeHelper.GetCatalogType(AppConstants.Media.MusicPublicationCodeOsg));
    }

    [Fact]
    public void AppConstants_logging_and_media_codes_are_non_empty()
    {
        Assert.Equal("nwt", AppConstants.Media.BiblePublicationCodeNwt);
        Assert.False(string.IsNullOrEmpty(
            AppConstants.Logging.MauiPlatformUiDiagnosticsLog.KeyboardHelperFailedToHideKeyboardAndroid));
    }

    [Fact]
    public void MagazineHelper_parse_build_section_year_and_filters()
    {
        var (apiPub, issue) = MagazineHelper.ParseSectionCode("20080115-wp");
        Assert.Equal("wp", apiPub);
        Assert.Equal("20080115", issue);
        Assert.Equal("20080115-wp", MagazineHelper.BuildSectionCode(apiPub, issue));

        var y = MagazineHelper.MagazineStartYear;
        Assert.Equal(y, MagazineHelper.GetYear($"w{y}"));
        Assert.True(MagazineHelper.IsMagazinePublicationCode($"w{y}"));
        Assert.True(MagazineHelper.IsWatchtowerMagazineCode($"w{y}"));
        Assert.True(MagazineHelper.IsAwakeMagazineCode($"g{y}"));
        Assert.False(MagazineHelper.IsMagazinePublicationCode($"w{y - 20}"));

        Assert.Throws<ArgumentException>(() => MagazineHelper.ParseSectionCode("nodashatall"));
    }

    [Fact]
    public void MagazineHelper_build_section_name_and_MediaTrackTitleHelper_decode()
    {
        Assert.Equal("Title — Date", MagazineHelper.BuildSectionName("Title", "Date"));
        Assert.Equal("Only", MagazineHelper.BuildSectionName("Only", null));
        Assert.Equal(MediaTrackTitleHelper.UnknownTitle, MediaTrackTitleHelper.DecodeHtmlTitle(null));
        Assert.Null(MediaTrackTitleHelper.DecodeHtmlTitleNullable(null));
        Assert.Equal("A & B", MediaTrackTitleHelper.DecodeHtmlTitle("A &amp; B"));
    }

    [Fact]
    public void SectionCodeHelper_normalize_compare_and_code_equals()
    {
        Assert.Null(SectionCodeHelper.Normalize("   "));
        Assert.Equal("iam-1", SectionCodeHelper.Normalize("  iam-1  "));
        Assert.True(SectionCodeHelper.CodeEquals(" 01 ", "1"));
        Assert.True(SectionCodeHelper.SectionCodeComparer.Compare("2", "10") < 0);
    }

    [Fact]
    public void BiblePublicationTrack_orders_by_track_code_equals_by_id()
    {
        var lang = new Language { Id = 1, LanguageCode = "E", Direction = "ltr" };
        var publication = new BiblePublication
        {
            Id = 1,
            Name = "NWT",
            PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            LanguageId = 1,
            Language = lang,
        };
        var left = new BiblePublicationTrack
        {
            Id = 10,
            TrackCode = "02",
            Title = "A",
            BiblePublicationId = 1,
            Publication = publication,
        };
        var right = new BiblePublicationTrack
        {
            Id = 10,
            TrackCode = "2",
            Title = "B",
            BiblePublicationId = 1,
            Publication = publication,
        };
        Assert.Equal(0, left.CompareTo(right));
        Assert.True(left.Equals(right));

        var chap10 = new BiblePublicationTrack
        {
            Id = 11,
            TrackCode = "10",
            Title = "",
            BiblePublicationId = 1,
            Publication = publication,
        };
        var chap2 = new BiblePublicationTrack
        {
            Id = 12,
            TrackCode = "2",
            Title = "",
            BiblePublicationId = 1,
            Publication = publication,
        };
        Assert.True(chap2 < chap10);
        Assert.Equal(1, chap2.CompareTo((object)"x"));
    }

    [Fact]
    public void JwSourceHelper_detects_music_publications_and_bible_seed_set()
    {
        Assert.False(JwSourceHelper.IsMusicPublicationCode(null));
        Assert.False(JwSourceHelper.IsMusicPublicationCode("  "));
        Assert.False(JwSourceHelper.IsMusicPublicationCode(AppConstants.Media.MediatorPublicationCodeMakingMusic));
        Assert.True(JwSourceHelper.IsMusicPublicationCode(AppConstants.Media.MusicPublicationCodeOsg));
        Assert.Contains(AppConstants.Media.BiblePublicationCodeNwt, JwSourceHelper.BiblePublicationCodes);
    }

    [Fact]
    public void Language_orders_by_code_and_equals_by_id()
    {
        var en = new Language { Id = 1, LanguageCode = "E", Direction = "ltr" };
        var fr = new Language { Id = 2, LanguageCode = "F", Direction = "ltr" };
        Assert.True(en < fr);
        Assert.Equal(1, en.CompareTo((object)"not-a-language"));

        Assert.True(en.Equals(new Language { Id = 1, LanguageCode = "Z", Direction = "ltr" }));
    }

    [Fact]
    public void TrackCodeComparer_orders_numeric_track_codes_naturally()
    {
        Assert.True(TrackCodeComparer.Comparer.Compare("2", "10") < 0);
        Assert.True(TrackCodeComparer.Comparer.Compare("b", "a") > 0);
    }

    [Fact]
    public void MusicTrackLookupHelper_resolves_code_and_handles_edge_cases()
    {
        var dict = new Dictionary<int, MusicTrack>
        {
            [7] = new MusicTrack { TrackCode = "01", Title = "a" },
            [8] = new MusicTrack { TrackCode = "2", Title = "b" },
        };

        Assert.True(MusicTrackLookupHelper.TryGetByCode(dict, "1", out var pair));
        Assert.Equal(7, pair.Key);

        Assert.False(MusicTrackLookupHelper.TryGetByCode(dict, "  ", out _));
        Assert.False(MusicTrackLookupHelper.TryGetByCode(new Dictionary<int, MusicTrack>(), "1", out _));

        Assert.Equal(7, MusicTrackLookupHelper.GetKeyByCode(dict, "01"));
        Assert.Null(MusicTrackLookupHelper.GetKeyByCode(dict, "__missing__"));
    }

    [Fact]
    public void BiblePublication_primary_category_none_when_no_categories_linked()
    {
        var bp = new BiblePublication
        {
            Id = 1,
            Name = "Pub",
            PublicationCode = "code",
            LanguageId = 1,
            Language = new Language { Id = 1, LanguageCode = "E", Direction = "ltr" },
        };

        Assert.Null(bp.PrimaryCategory);
        Assert.Equal(0, bp.PrimaryCategoryId);
    }

    [Fact]
    public void AlarmSchedule_cron_expression_reflects_time_and_weekdays()
    {
        var schedule = new AlarmSchedule
        {
            Hour = 9,
            Minute = 15,
            Second = 30,
            DaysOfWeek = WeekDays.Monday | WeekDays.Friday,
        };

        var cron = schedule.CronExpression;
        Assert.Contains("9", cron);
        Assert.Contains("15", cron);
        Assert.Contains("30", cron);
    }
}
