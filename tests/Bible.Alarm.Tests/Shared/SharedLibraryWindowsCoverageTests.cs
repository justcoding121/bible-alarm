#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;

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
}
