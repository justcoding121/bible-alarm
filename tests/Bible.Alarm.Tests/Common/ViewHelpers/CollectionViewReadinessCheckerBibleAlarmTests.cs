#nullable enable

using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Tests.Support;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class CollectionViewReadinessCheckerBibleAlarmTests(MauiUiFixture fixture)
{
    [Fact]
    public async Task WaitForCollectionViewReadyAsync_returns_false_when_collection_view_has_no_items_source()
    {
        _ = fixture;
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
    public async Task WaitForCollectionViewReadyAsync_returns_false_when_item_not_in_items_source()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var collectionView = new CollectionView
        {
            ItemsSource = new List<string> { "alpha", "beta" },
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var ready = await CollectionViewReadinessChecker.WaitForCollectionViewReadyAsync(
            collectionView,
            "gamma",
            cts.Token);

        Assert.False(ready);
    }

    [Fact]
    public async Task WaitForCollectionViewReadyAsync_returns_true_when_items_source_contains_item_and_handler_attached()
    {
        if (!MauiUiTestHostHelper.CanUseVisualTree)
        {
            return;
        }

        if (!await MauiUiTestHostHelper.EnsurePrimaryWindowAsync())
        {
            return;
        }

        const string target = "scroll-target";

        bool? ready = null;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var collectionView = new CollectionView
            {
                ItemsSource = new List<string> { "other", target },
            };
            Application.Current!.Windows[0].Page = new ContentPage { Content = collectionView };

            for (var attempt = 0; attempt < 30 && collectionView.Handler?.PlatformView is null; attempt++)
            {
                await Task.Delay(100);
            }

            if (collectionView.Handler?.PlatformView is null)
            {
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            ready = await CollectionViewReadinessChecker.WaitForCollectionViewReadyAsync(
                collectionView,
                target,
                cts.Token);
        });

        if (ready is null)
        {
            return;
        }

        Assert.True(ready.Value);
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

    [Fact]
    public async Task WaitForCollectionViewReadyAsync_returns_false_for_null_items_source()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var collectionView = new CollectionView { ItemsSource = null };

        var ready = await CollectionViewReadinessChecker.WaitForCollectionViewReadyAsync(
            collectionView,
            "any",
            cts.Token);

        Assert.False(ready);
    }
}
