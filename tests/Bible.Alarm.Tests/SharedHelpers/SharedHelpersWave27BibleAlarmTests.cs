#nullable enable

using System.Text.Json;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class RepeatingFireOccurrencePlannerBibleAlarmTests
{
    [Fact]
    public void CollectFutureOccurrences_collects_only_fires_strictly_inside_horizon_window()
    {
        var anchor = new DateTimeOffset(2035, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var results = RepeatingFireOccurrencePlanner.CollectFutureOccurrences(
            c => c.AddHours(1),
            () => anchor,
            TimeSpan.FromHours(3),
            maxIterations: 20);

        Assert.Equal(
            new[] { anchor.AddHours(1), anchor.AddHours(2), anchor.AddHours(3) },
            results);
    }

    [Fact]
    public void CollectFutureOccurrences_skips_fires_not_after_latest_clock_reading_before_collecting()
    {
        var anchor = new DateTimeOffset(2038, 2, 3, 12, 0, 0, TimeSpan.Zero);
        var past = anchor.AddHours(-1);

        DateTimeOffset Next(DateTimeOffset cursor)
        {
            if (cursor == anchor)
            {
                return past;
            }

            if (cursor == past)
            {
                return anchor.AddHours(1);
            }

            return anchor.AddDays(30);
        }

        var results = RepeatingFireOccurrencePlanner.CollectFutureOccurrences(
            Next,
            () => anchor,
            TimeSpan.FromHours(24),
            maxIterations: 20);

        Assert.Equal(anchor.AddHours(1), Assert.Single(results));
    }

    [Fact]
    public void CollectFutureOccurrences_throws_when_next_fire_delegate_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RepeatingFireOccurrencePlanner.CollectFutureOccurrences(
                null!,
                () => DateTimeOffset.UtcNow,
                TimeSpan.FromDays(1),
                maxIterations: 1));
    }

    [Fact]
    public void CollectFutureOccurrences_throws_when_clock_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RepeatingFireOccurrencePlanner.CollectFutureOccurrences(
                c => c.AddMinutes(1),
                null!,
                TimeSpan.FromDays(1),
                maxIterations: 1));
    }
}

public sealed class CancellationSourceExclusiveReplacementBibleAlarmTests
{
    [Fact]
    public void TryTakeExclusive_returns_false_when_slot_already_null()
    {
        CancellationTokenSource? slot = null;
        Assert.False(CancellationSourceExclusiveReplacement.TryTakeExclusive(ref slot, out var taken));
        Assert.Null(taken);
        Assert.Null(slot);
    }

    [Fact]
    public void TryTakeExclusive_nulls_slot_and_yields_prior_instance_for_teardown()
    {
        using var owned = new CancellationTokenSource();
        CancellationTokenSource? slot = owned;

        Assert.True(CancellationSourceExclusiveReplacement.TryTakeExclusive(ref slot, out var taken));
        Assert.Same(owned, taken);
        Assert.Null(slot);
    }

    [Fact]
    public void CancelDisposeSwallowDisposed_swallows_ObjectDisposedException_when_source_already_disposed()
    {
        var cts = new CancellationTokenSource();
        cts.Dispose();

        var ex = Record.Exception(() => CancellationSourceExclusiveReplacement.CancelDisposeSwallowDisposed(cts));
        Assert.Null(ex);
    }

    [Fact]
    public void CancelDisposeSwallowDisposed_cancels_and_disposes_active_source()
    {
        var cts = new CancellationTokenSource();

        CancellationSourceExclusiveReplacement.CancelDisposeSwallowDisposed(cts);

        Assert.True(cts.IsCancellationRequested);
    }
}

public sealed class SequentialMatchingInvokerBibleAlarmTests
{
    [Fact]
    public void InvokeEachMatching_counts_every_matching_item_even_when_invoke_throws()
    {
        var failedKeys = new List<int>();

        var matchedCount = SequentialMatchingInvoker.InvokeEachMatching(
            new[] { 1, 2, 3 },
            n => n % 2 == 1,
            n =>
            {
                if (n == 3)
                {
                    throw new InvalidOperationException();
                }
            },
            (n, _) => failedKeys.Add(n));

        Assert.Equal(2, matchedCount);
        Assert.Equal(new[] { 3 }, failedKeys);
    }

    [Fact]
    public void InvokeEachMatching_skips_items_that_fail_predicate_without_calling_invoke()
    {
        var invoked = new List<char>();

        var matchedCount = SequentialMatchingInvoker.InvokeEachMatching(
            new[] { 'a', 'b' },
            ch => ch == 'z',
            ch => invoked.Add(ch),
            null);

        Assert.Equal(0, matchedCount);
        Assert.Empty(invoked);
    }

    [Fact]
    public void InvokeEachMatching_throws_when_sequence_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SequentialMatchingInvoker.InvokeEachMatching<object>(
                sequence: null!,
                _ => true,
                _ => { },
                null));
    }
}

public sealed class DirectoryHelperBibleAlarmTests
{
    [Fact]
    public void IndexDirectory_resolves_repo_tools_index_folder()
    {
        var path = DirectoryHelper.IndexDirectory;

        Assert.Contains("bible-alarm", path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine("_tools", "_index"), path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ensure_creates_directory_when_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ba-dir-helper-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(dir));

        try
        {
            DirectoryHelper.Ensure(dir);

            Assert.True(Directory.Exists(dir));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}

public sealed class DisposableOneShotGateBibleAlarmTests
{
    [Fact]
    public void TryBegin_allows_first_transition_then_blocks_follow_up_calls()
    {
        var disposed = false;

        Assert.True(DisposableOneShotGate.TryBegin(ref disposed));
        Assert.True(disposed);
        Assert.False(DisposableOneShotGate.TryBegin(ref disposed));
    }
}

public sealed class MediatorVideoPublicationCategoryKeyMapperBibleAlarmTests
{
    [Fact]
    public void TryGetCategoryKey_returns_false_for_unmapped_publication_code()
    {
        Assert.False(MediatorVideoPublicationCategoryKeyMapper.TryGetCategoryKey("not-a-mediator-video", out _));
    }

    [Fact]
    public void TryGetCategoryKey_returns_dramas_good_news_mediator_key()
    {
        Assert.True(MediatorVideoPublicationCategoryKeyMapper.TryGetCategoryKey(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            out var key));

        Assert.Equal(AppConstants.Media.BiblePublicationCodeDramasGoodNews, key);
    }

    [Fact]
    public void TryGetCategoryKey_accepts_upper_cased_normalized_code()
    {
        Assert.True(MediatorVideoPublicationCategoryKeyMapper.TryGetCategoryKey(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews.ToUpperInvariant(),
            out var key));

        Assert.Equal(AppConstants.Media.BiblePublicationCodeDramasGoodNews, key);
    }

    [Fact]
    public void TryGetCategoryKey_throws_when_code_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() => MediatorVideoPublicationCategoryKeyMapper.TryGetCategoryKey(" ", out _));
    }

    [Theory]
    [InlineData(AppConstants.Media.NormalizedPublicationCodeVODMoviesBibleTimes, AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes)]
    [InlineData(AppConstants.Media.NormalizedPublicationCodeVODMoviesModernDay, AppConstants.Media.BiblePublicationCodeVODMoviesModernDay)]
    [InlineData(AppConstants.Media.NormalizedPublicationCodeVODMoviesAnimated, AppConstants.Media.BiblePublicationCodeVODMoviesAnimated)]
    [InlineData(AppConstants.Media.NormalizedPublicationCodeVODMoviesExtras, AppConstants.Media.BiblePublicationCodeVODMoviesExtras)]
    [InlineData(AppConstants.Media.NormalizedPublicationCodeSeriesDigForTreasures, AppConstants.Media.BiblePublicationCodeSeriesDigForTreasures)]
    [InlineData(AppConstants.Media.NormalizedPublicationCodeSeriesBJFLessons, AppConstants.Media.BiblePublicationCodeSeriesBJFLessons)]
    public void TryGetCategoryKey_maps_remaining_mediator_video_codes(string normalized, string expectedKey)
    {
        Assert.True(MediatorVideoPublicationCategoryKeyMapper.TryGetCategoryKey(normalized, out var key));
        Assert.Equal(expectedKey, key);
    }
}

public sealed class MediatorVideoLocalizedCategoryNameReaderBibleAlarmTests
{
    [Fact]
    public void TryReadLocalizedDisplayName_returns_false_when_category_property_missing()
    {
        using var doc = JsonDocument.Parse("{}");

        Assert.False(MediatorVideoLocalizedCategoryNameReader.TryReadLocalizedDisplayName(doc.RootElement, out _));
    }

    [Fact]
    public void TryReadLocalizedDisplayName_returns_false_when_category_object_missing_name_property()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        using var doc = JsonDocument.Parse($"{{\"{cat}\":{{}}}}");

        Assert.False(MediatorVideoLocalizedCategoryNameReader.TryReadLocalizedDisplayName(doc.RootElement, out _));
    }

    [Fact]
    public void TryReadLocalizedDisplayName_returns_true_when_category_name_present_and_nonempty()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        using var doc = JsonDocument.Parse($"{{\"{cat}\":{{\"{nm}\":\"Hello Video\"}}}}");

        Assert.True(MediatorVideoLocalizedCategoryNameReader.TryReadLocalizedDisplayName(doc.RootElement, out var name));

        Assert.Equal("Hello Video", name);
    }

    [Fact]
    public void TryReadLocalizedDisplayName_decodes_nbsp_entities_before_accepting_value()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        using var doc = JsonDocument.Parse($"{{\"{cat}\":{{\"{nm}\":\"A&nbsp;B\"}}}}");

        Assert.True(MediatorVideoLocalizedCategoryNameReader.TryReadLocalizedDisplayName(doc.RootElement, out var name));

        Assert.Equal("A B", name);
    }

    [Fact]
    public void TryReadLocalizedDisplayName_returns_false_when_decoded_name_empty()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        using var doc = JsonDocument.Parse($"{{\"{cat}\":{{\"{nm}\":\"\"}}}}");

        Assert.False(MediatorVideoLocalizedCategoryNameReader.TryReadLocalizedDisplayName(doc.RootElement, out _));
    }
}
