#nullable enable

using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Tests.Support;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class CollectionViewReadinessCheckerBibleAlarmTests
{
    [Fact]
    public async Task WaitForCollectionViewReadyAsync_returns_false_when_collection_view_has_no_items_source()
    {
        MauiUiTestBootstrap.TryInitialize();
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var collectionView = new CollectionView();

        var ready = await CollectionViewReadinessChecker.WaitForCollectionViewReadyAsync(
            collectionView,
            item: "missing",
            cts.Token);

        Assert.False(ready);
    }

    [Fact]
    public async Task WaitForCollectionViewReadyAsync_returns_true_when_items_source_contains_item_and_handler_attached()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        const string target = "scroll-target";
        var collectionView = new CollectionView
        {
            ItemsSource = new List<string> { "other", target },
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var ready = await CollectionViewReadinessChecker.WaitForCollectionViewReadyAsync(
            collectionView,
            target,
            cts.Token);

        Assert.True(ready);
    }

    [Fact]
    public async Task WaitForCollectionViewReadyAsync_honors_cancellation()
    {
        MauiUiTestBootstrap.TryInitialize();
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var collectionView = new CollectionView { ItemsSource = new List<string> { "x" } };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CollectionViewReadinessChecker.WaitForCollectionViewReadyAsync(collectionView, "x", cts.Token));
    }
}
