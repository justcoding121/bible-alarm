#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Xunit;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionInitHandlerTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class IdleLanguageNameService : ILanguageNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<int, string>());

        public string? GetNameCached(int languageId) => null;

        public string? GetNameByLanguageCodeCached(string languageCode) => null;
    }

    private static MusicPublicationSelectionInitHandler CreateHandler(
        MusicPublicationSelectionStateManager stateManager,
        System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel> languages,
        Action<LanguageListViewItemModel?>? setCurrentLanguage = null,
        LanguageListViewItemModel? currentLanguage = null)
    {
        LanguageListViewItemModel? current = currentLanguage;
        var dataProvider = new MusicPublicationSelectionDataProvider(new IdleCatalogMediaService(), new IdleLanguageNameService());
        return new MusicPublicationSelectionInitHandler(
            new IdleCatalogMediaService(),
            stateManager,
            dataProvider,
            () => languages,
            () => current,
            lang =>
            {
                current = lang;
                setCurrentLanguage?.Invoke(lang);
            },
            _ => { });
    }

    [Fact]
    public async Task InitializeInternalAsync_returns_without_calling_populate_when_current_uninitialized()
    {
        var languages = new System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>();
        var handler = CreateHandler(new MusicPublicationSelectionStateManager(), languages);

        var langCalls = 0;
        var songCalls = 0;

        await handler.InitializeInternalAsync(
            new FakeApplicationState(new ApplicationState()),
            _ => { langCalls++; return Task.CompletedTask; },
            (_, _, _, _) => { songCalls++; return Task.CompletedTask; },
            _ => Task.CompletedTask);

        Assert.Equal(0, langCalls);
        Assert.Equal(0, songCalls);
    }

    [Fact]
    public async Task InitializeInternalAsync_populates_song_publications_only_when_language_missing()
    {
        var stateManager = new MusicPublicationSelectionStateManager();
        stateManager.InitializeCurrent(new FakeApplicationState(new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem
            {
                MusicPublicationCode = "osg",
                MusicLanguageCode = null
            }
        }));

        var languages = new System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>();
        var handler = CreateHandler(stateManager, languages);

        var langCalls = 0;
        string? songArg = "unset";
        var songCalls = 0;

        await handler.InitializeInternalAsync(
            new FakeApplicationState(new ApplicationState()),
            _ => { langCalls++; return Task.CompletedTask; },
            (code, warm, _, _) => { songArg = code; songCalls++; return Task.CompletedTask; },
            _ => Task.CompletedTask);

        Assert.Equal(0, langCalls);
        Assert.Equal(1, songCalls);
        Assert.Null(songArg);
    }

    [Fact]
    public async Task InitializeInternalAsync_populates_languages_first_when_list_empty()
    {
        var stateManager = new MusicPublicationSelectionStateManager();
        stateManager.InitializeCurrent(new FakeApplicationState(new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem
            {
                MusicPublicationCode = "osg",
                MusicLanguageCode = "E"
            }
        }));

        var languages = new System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>();
        var handler = CreateHandler(stateManager, languages);

        var sequence = new List<string>();

        await handler.InitializeInternalAsync(
            new FakeApplicationState(new ApplicationState()),
            _ => { sequence.Add("lang"); return Task.CompletedTask; },
            (_, _, _, _) => { sequence.Add("song"); return Task.CompletedTask; },
            _ => Task.CompletedTask);

        Assert.Equal(new[] { "lang", "song" }, sequence);
    }

    [Fact]
    public void UpdateSelectedLanguage_clears_previous_selection_and_selects_new()
    {
        var languages = new System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>();
        LanguageListViewItemModel? current = null;
        var handler = CreateHandler(
            new MusicPublicationSelectionStateManager(),
            languages,
            lang => current = lang);

        var first = new LanguageListViewItemModel(new Language { LanguageCode = "E" }, "English") { IsSelected = true };
        var second = new LanguageListViewItemModel(new Language { LanguageCode = "S" }, "Spanish") { IsSelected = false };

        handler.UpdateSelectedLanguage(first);
        handler.UpdateSelectedLanguage(second);

        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
        Assert.Same(second, current);
    }
}
