#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class ScheduleListItemBibleDisplayNameProviderTests
{
    private sealed class FakeAppState : IState<ApplicationState>
    {
        public FakeAppState(ApplicationState value) => Value = value;

        public ApplicationState Value { get; }

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class StubCategoryNameService : ICategoryNameService
    {
        public string? GetName(string categoryCode, string languageCode) => $"Loc-{categoryCode}";

        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static ApplicationState StateWithSchedule(ScheduleStateItem item)
    {
        var set = new ObservableHashSet<ScheduleStateItem>();
        set.Add(item);
        return new ApplicationState(set, currentSchedule: item);
    }

    [Fact]
    public void GetCategoryCode_empty_for_invalid_schedule_id()
    {
        var sut = new ScheduleListItemBibleDisplayNameProvider(
            new FakeAppState(StateWithSchedule(new ScheduleStateItem { Id = 5 })),
            new StubCategoryNameService());

        Assert.Equal(string.Empty, sut.GetCategoryCode(0));
        Assert.Equal(string.Empty, sut.GetCategoryCode(-1));
    }

    [Fact]
    public void GetCategoryCode_uses_schedule_category_or_publication_mapping()
    {
        var item = new ScheduleStateItem
        {
            Id = 10,
            BiblePublicationCategoryName = "Videos",
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
        };
        var sut = new ScheduleListItemBibleDisplayNameProvider(
            new FakeAppState(StateWithSchedule(item)),
            new StubCategoryNameService());

        Assert.Equal("Videos", sut.GetCategoryCode(10));

        item.BiblePublicationCategoryName = null;
        Assert.NotEmpty(sut.GetCategoryCode(10));
    }

    [Fact]
    public void GetCategoryDisplayName_resolves_via_category_service()
    {
        var item = new ScheduleStateItem { Id = 3, BiblePublicationCategoryName = "Bible" };
        var sut = new ScheduleListItemBibleDisplayNameProvider(
            new FakeAppState(StateWithSchedule(item)),
            new StubCategoryNameService());

        Assert.Equal("Loc-Bible", sut.GetCategoryDisplayName(3));
    }

    [Fact]
    public void IsBibleCategory_matches_constant()
    {
        var bible = new ScheduleStateItem { Id = 1, BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible };
        var other = new ScheduleStateItem { Id = 2, BiblePublicationCategoryName = "Music" };
        var set = new ObservableHashSet<ScheduleStateItem>();
        set.Add(bible);
        set.Add(other);
        var state = new ApplicationState(set);
        var sut = new ScheduleListItemBibleDisplayNameProvider(new FakeAppState(state), new StubCategoryNameService());

        Assert.True(sut.IsBibleCategory(1));
        Assert.False(sut.IsBibleCategory(2));
    }

    [Fact]
    public void GetBiblePublicationSectionAndTrackOneLine_bible_category_joins_section_and_track()
    {
        var item = new ScheduleStateItem
        {
            Id = 7,
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
            BiblePublicationSectionName = "Genesis",
            BiblePublicationTrackCode = "1",
        };
        var sut = new ScheduleListItemBibleDisplayNameProvider(
            new FakeAppState(StateWithSchedule(item)),
            new StubCategoryNameService());

        Assert.Equal("Genesis 1", sut.GetBiblePublicationSectionAndTrackOneLine(7));
    }

    [Fact]
    public void GetBiblePublicationSectionAndTrackOneLine_non_bible_returns_empty()
    {
        var item = new ScheduleStateItem
        {
            Id = 8,
            BiblePublicationCategoryName = "Music",
            BiblePublicationSectionName = "Disc",
            BiblePublicationTrackCode = "1",
        };
        var sut = new ScheduleListItemBibleDisplayNameProvider(
            new FakeAppState(StateWithSchedule(item)),
            new StubCategoryNameService());

        Assert.Equal(string.Empty, sut.GetBiblePublicationSectionAndTrackOneLine(8));
    }

    [Fact]
    public void GetBiblePublicationTrackName_prefers_track_title()
    {
        var item = new ScheduleStateItem
        {
            Id = 9,
            BiblePublicationTrackTitle = "Chapter 5",
            BiblePublicationTrackCode = "5",
        };
        var sut = new ScheduleListItemBibleDisplayNameProvider(
            new FakeAppState(StateWithSchedule(item)),
            new StubCategoryNameService());

        Assert.Equal("Chapter 5", sut.GetBiblePublicationTrackName(9));
    }
}
