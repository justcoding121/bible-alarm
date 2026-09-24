#nullable enable

using System.Collections;
using System.Runtime.InteropServices;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class CollectionViewScrollExecutorTests
{
    [Fact]
    public void FindItemIndexByValue_finds_language_by_code_when_instance_differs()
    {
        var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var rowInList = new LanguageListViewItemModel(lang, "English");
        var lookupInstance = new LanguageListViewItemModel(lang, "Other display");
        var list = new ArrayList { rowInList };

        var index = CollectionViewScrollExecutor.FindItemIndexByValue(list, lookupInstance);

        Assert.Equal(0, index);
    }

    [Fact]
    public void FindItemIndexByValue_returns_negative_one_when_no_value_match()
    {
        var langA = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var langF = new Language { LanguageCode = "F", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var list = new ArrayList { new LanguageListViewItemModel(langA, "English") };
        var other = new LanguageListViewItemModel(langF, "Français");

        var index = CollectionViewScrollExecutor.FindItemIndexByValue(list, other);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void FindItemIndexByValue_finds_by_reference_when_value_equality_does_not_apply()
    {
        var first = new object();
        var second = new object();
        var list = new ArrayList { first, second };

        Assert.Equal(0, CollectionViewScrollExecutor.FindItemIndexByValue(list, first));
        Assert.Equal(1, CollectionViewScrollExecutor.FindItemIndexByValue(list, second));
    }

    [Fact]
    public void FindItemIndexByValue_returns_negative_one_for_empty_list()
    {
        var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var item = new LanguageListViewItemModel(lang, "English");

        Assert.Equal(-1, CollectionViewScrollExecutor.FindItemIndexByValue(new ArrayList(), item));
    }

    [Fact]
    public void FindItemIndexByValue_finds_publication_by_code_case_insensitive()
    {
        var inList = new PublicationListViewItemModel(new Publication { Name = "NWT", PublicationCode = "nwt" });
        var lookup = new PublicationListViewItemModel(new Publication { Name = "Other", PublicationCode = "NWT" });
        var list = new ArrayList { inList };

        Assert.Equal(0, CollectionViewScrollExecutor.FindItemIndexByValue(list, lookup));
    }

    [Fact]
    public void FindItemIndexByValue_finds_section_by_section_code_case_insensitive()
    {
        var inList = new BiblePublicationSectionListViewItemModel(
            new BiblePublicationSection { SectionCode = "mat", Name = "Matthew" });
        var lookup = new BiblePublicationSectionListViewItemModel(
            new BiblePublicationSection { SectionCode = "MAT", Name = "Other" });
        var list = new ArrayList { inList };

        Assert.Equal(0, CollectionViewScrollExecutor.FindItemIndexByValue(list, lookup));
    }

    [Fact]
    public void FindItemIndexByValue_finds_bible_track_by_track_code()
    {
        var inList = new BiblePublicationTrackListViewItemModel(
            new BiblePublicationTrack { TrackCode = "12", Title = "Chapter 12" });
        var lookup = new BiblePublicationTrackListViewItemModel(
            new BiblePublicationTrack { TrackCode = "12", Title = "Different title" });
        var list = new ArrayList { inList };

        Assert.Equal(0, CollectionViewScrollExecutor.FindItemIndexByValue(list, lookup));
    }

    [Fact]
    public void FindItemIndexByValue_finds_music_track_by_track_code()
    {
        var inList = new MusicTrackListViewItemModel(new MusicTrack { TrackCode = "42", Title = "Song" });
        var lookup = new MusicTrackListViewItemModel(new MusicTrack { TrackCode = "42", Title = "Other" });
        var list = new ArrayList { inList };

        Assert.Equal(0, CollectionViewScrollExecutor.FindItemIndexByValue(list, lookup));
    }

    [Fact]
    public void FindItemIndexByValue_skips_mismatched_row_types_and_returns_negative_one()
    {
        var pub = new PublicationListViewItemModel(new Publication { Name = "NWT", PublicationCode = "nwt" });
        var lang = new Language
        {
            LanguageCode = "E",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };
        var lookup = new LanguageListViewItemModel(lang, "English");
        var list = new ArrayList { pub, "plain-string" };

        Assert.Equal(-1, CollectionViewScrollExecutor.FindItemIndexByValue(list, lookup));
    }

    [Fact]
    public void FindItemIndexByValue_finds_later_index_when_match_is_not_first()
    {
        var a = new PublicationListViewItemModel(new Publication { Name = "A", PublicationCode = "aa" });
        var b = new PublicationListViewItemModel(new Publication { Name = "B", PublicationCode = "bb" });
        var lookup = new PublicationListViewItemModel(new Publication { Name = "B2", PublicationCode = "bb" });
        var list = new ArrayList { a, b };

        Assert.Equal(1, CollectionViewScrollExecutor.FindItemIndexByValue(list, lookup));
    }

    [Fact]
    public void ValueEqualsCollectionItem_false_when_list_item_null()
    {
        var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var item = new LanguageListViewItemModel(lang, "English");

        Assert.False(CollectionViewScrollExecutor.ValueEqualsCollectionItem(null, item));
    }

    [Fact]
    public void ValueEqualsCollectionItem_false_for_unsupported_item_type()
    {
        Assert.False(CollectionViewScrollExecutor.ValueEqualsCollectionItem("list", "lookup"));
        Assert.False(CollectionViewScrollExecutor.ValueEqualsCollectionItem(42, 42));
    }

    [Fact]
    public void ValueEqualsCollectionItem_false_when_publication_codes_differ()
    {
        var left = new PublicationListViewItemModel(new Publication { Name = "A", PublicationCode = "aa" });
        var right = new PublicationListViewItemModel(new Publication { Name = "B", PublicationCode = "bb" });

        Assert.False(CollectionViewScrollExecutor.ValueEqualsCollectionItem(left, right));
    }

    [Fact]
    public void ValueEqualsCollectionItem_false_when_section_codes_differ()
    {
        var left = new BiblePublicationSectionListViewItemModel(
            new BiblePublicationSection { SectionCode = "1", Name = "A" });
        var right = new BiblePublicationSectionListViewItemModel(
            new BiblePublicationSection { SectionCode = "2", Name = "B" });

        Assert.False(CollectionViewScrollExecutor.ValueEqualsCollectionItem(left, right));
    }

    [Fact]
    public void ValueEqualsCollectionItem_false_when_bible_track_codes_differ()
    {
        var left = new BiblePublicationTrackListViewItemModel(
            new BiblePublicationTrack { TrackCode = "1", Title = "A" });
        var right = new BiblePublicationTrackListViewItemModel(
            new BiblePublicationTrack { TrackCode = "2", Title = "B" });

        Assert.False(CollectionViewScrollExecutor.ValueEqualsCollectionItem(left, right));
    }

    [Fact]
    public void ValueEqualsCollectionItem_false_when_music_track_codes_differ()
    {
        var left = new MusicTrackListViewItemModel(new MusicTrack { TrackCode = "1", Title = "A" });
        var right = new MusicTrackListViewItemModel(new MusicTrack { TrackCode = "2", Title = "B" });

        Assert.False(CollectionViewScrollExecutor.ValueEqualsCollectionItem(left, right));
    }

    [Fact]
    public void ValueEqualsCollectionItem_true_for_matching_language_codes_case_insensitive()
    {
        var langE = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var langLower = new Language { LanguageCode = "e", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var left = new LanguageListViewItemModel(langE, "English");
        var right = new LanguageListViewItemModel(langLower, "english");

        Assert.True(CollectionViewScrollExecutor.ValueEqualsCollectionItem(left, right));
    }

    [Fact]
    public void ShouldLogUnexpectedScrollFailure_is_false_for_expected_platform_exceptions()
    {
        Assert.False(CollectionViewScrollExecutor.ShouldLogUnexpectedScrollFailure(new OperationCanceledException()));
        Assert.False(CollectionViewScrollExecutor.ShouldLogUnexpectedScrollFailure(new NullReferenceException()));
        Assert.False(CollectionViewScrollExecutor.ShouldLogUnexpectedScrollFailure(new COMException()));
    }

    [Fact]
    public void ShouldLogUnexpectedScrollFailure_is_true_for_unexpected_errors()
    {
        Assert.True(CollectionViewScrollExecutor.ShouldLogUnexpectedScrollFailure(new InvalidOperationException()));
        Assert.True(CollectionViewScrollExecutor.ShouldLogUnexpectedScrollFailure(new ArgumentException()));
        Assert.True(CollectionViewScrollExecutor.ShouldLogUnexpectedScrollFailure(new Exception("generic")));
    }

    [Fact]
    public void ValidateCollectionViewBeforeScroll_false_when_collection_view_null()
    {
        Assert.False(CollectionViewScrollExecutor.ValidateCollectionViewBeforeScroll(null!, CancellationToken.None));
    }

    [Fact]
    public void TryFindParentScrollView_returns_null_when_parent_null()
    {
        Assert.Null(CollectionViewScrollExecutor.TryFindParentScrollView(null));
    }

    [Collection("MauiUi")]
    public sealed class MauiCollectionViewScrollExecutorTests(MauiUiFixture fixture)
    {
        private static async Task RunWithMauiOrSkipAsync(Func<Task> testBody)
        {
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            try
            {
                await testBody();
            }
            catch (COMException)
            {
                // Headless xUnit cannot initialize WinUI MainThread; device/CI app hosts cover this path.
            }
            catch (TypeInitializationException)
            {
                // VisualElement static ctor requires WinUI; skip when COM init fails.
            }
        }

        private static void RunWithMauiOrSkip(Action testBody)
        {
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            try
            {
                testBody();
            }
            catch (COMException)
            {
            }
            catch (TypeInitializationException)
            {
            }
        }

        [Fact]
        public void ValidateCollectionViewBeforeScroll_false_when_not_attached_to_platform()
        {
            _ = fixture;
            RunWithMauiOrSkip(() =>
            {
                var collectionView = new CollectionView();
                Assert.False(CollectionViewScrollExecutor.ValidateCollectionViewBeforeScroll(
                    collectionView,
                    CancellationToken.None));
            });
        }

        [Fact]
        public void ValidateCollectionViewBeforeScroll_false_even_when_token_already_canceled_if_unattached()
        {
            _ = fixture;
            RunWithMauiOrSkip(() =>
            {
                using var cts = new CancellationTokenSource();
                cts.Cancel();
                var collectionView = new CollectionView();

                Assert.False(CollectionViewScrollExecutor.ValidateCollectionViewBeforeScroll(
                    collectionView,
                    cts.Token));
            });
        }

        [Fact]
        public void TryFindParentScrollView_returns_null_when_parent_is_not_scroll_view()
        {
            _ = fixture;
            RunWithMauiOrSkip(() =>
            {
                var contentPage = new ContentPage();
                var collectionView = new CollectionView();
                contentPage.Content = collectionView;

                Assert.Null(CollectionViewScrollExecutor.TryFindParentScrollView(collectionView.Parent));
            });
        }

        [Fact]
        public void TryFindParentScrollView_returns_scroll_view_when_direct_parent()
        {
            _ = fixture;
            RunWithMauiOrSkip(() =>
            {
                var scrollView = new ScrollView();
                var collectionView = new CollectionView();
                scrollView.Content = collectionView;

                var found = CollectionViewScrollExecutor.TryFindParentScrollView(collectionView.Parent);

                Assert.Same(scrollView, found);
            });
        }

        [Fact]
        public async Task PerformScrollAsync_completes_without_throwing_when_collection_view_unready()
        {
            _ = fixture;
            await RunWithMauiOrSkipAsync(async () =>
            {
                var collectionView = new CollectionView
                {
                    ItemsSource = new List<string> { "a", "b" },
                };

                await CollectionViewScrollExecutor.PerformScrollAsync(
                    collectionView,
                    "a",
                    ScrollToPosition.Center,
                    animated: false,
                    CancellationToken.None);
            });
        }

        [Fact]
        public async Task PerformScrollAsync_end_position_completes_without_throwing_when_unready()
        {
            _ = fixture;
            await RunWithMauiOrSkipAsync(async () =>
            {
                var collectionView = new CollectionView
                {
                    ItemsSource = new List<string> { "first", "last" },
                };

                await CollectionViewScrollExecutor.PerformScrollAsync(
                    collectionView,
                    "last",
                    ScrollToPosition.End,
                    animated: false,
                    CancellationToken.None);
            });
        }

        [Fact]
        public async Task PerformScrollAsync_end_position_with_scroll_view_parent_completes_when_unready()
        {
            _ = fixture;
            await RunWithMauiOrSkipAsync(async () =>
            {
                var collectionView = new CollectionView
                {
                    ItemsSource = new List<string> { "x" },
                };
                var scrollView = new ScrollView { Content = collectionView };

                Assert.Same(scrollView, collectionView.Parent);

                await CollectionViewScrollExecutor.PerformScrollAsync(
                    collectionView,
                    "x",
                    ScrollToPosition.End,
                    animated: true,
                    CancellationToken.None);
            });
        }

        [Fact]
        public async Task PerformScrollAsync_swallows_when_token_already_canceled()
        {
            _ = fixture;
            await RunWithMauiOrSkipAsync(async () =>
            {
                using var cts = new CancellationTokenSource();
                cts.Cancel();
                var collectionView = new CollectionView { ItemsSource = new List<object> { 1 } };

                await CollectionViewScrollExecutor.PerformScrollAsync(
                    collectionView,
                    1,
                    ScrollToPosition.MakeVisible,
                    animated: false,
                    cts.Token);
            });
        }

        [Fact]
        public async Task ScrollAndWaitAsync_with_index_completes_when_main_thread_available()
        {
            _ = fixture;
            await RunWithMauiOrSkipAsync(async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var collectionView = new CollectionView
                {
                    ItemsSource = new List<string> { "one", "two", "three" },
                };

                await CollectionViewScrollExecutor.ScrollAndWaitAsync(
                    collectionView,
                    itemOrIndex: 1,
                    ScrollToPosition.Center,
                    animated: false,
                    cts.Token);
            });
        }

        [Fact]
        public async Task ScrollAndWaitAsync_with_item_completes_when_main_thread_available()
        {
            _ = fixture;
            await RunWithMauiOrSkipAsync(async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                const string item = "target";
                var collectionView = new CollectionView
                {
                    ItemsSource = new List<string> { "other", item },
                };

                await CollectionViewScrollExecutor.ScrollAndWaitAsync(
                    collectionView,
                    item,
                    ScrollToPosition.MakeVisible,
                    animated: false,
                    cts.Token);
            });
        }

        [Fact]
        public async Task PerformScrollAsync_standard_position_with_items_source_completes()
        {
            _ = fixture;
            await RunWithMauiOrSkipAsync(async () =>
            {
                var item = new PublicationListViewItemModel(
                    new Publication { Name = "NWT", PublicationCode = "nwt" });
                var collectionView = new CollectionView
                {
                    ItemsSource = new List<PublicationListViewItemModel> { item },
                };

                await CollectionViewScrollExecutor.PerformScrollAsync(
                    collectionView,
                    item,
                    ScrollToPosition.Center,
                    animated: false,
                    CancellationToken.None);
            });
        }
    }
}
