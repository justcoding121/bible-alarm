#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionPropertyManagerTests
{
    private static LanguageListViewItemModel LangRow(string code) =>
        new(
            new Language { LanguageCode = code, Direction = AppConstants.Media.TextDirectionLeftToRight },
            $"{code}-nm");

    [Fact]
    public void UpdateCurrentLanguageFromLanguages_syncs_From_IsSelected_marker()
    {
        var sut = new MusicPublicationSelectionPropertyManager();
        sut.Languages.Clear();
        var first = LangRow("A");
        var second = LangRow("B");

        sut.Languages.Add(first);
        sut.Languages.Add(second);
        second.IsSelected = true;

        sut.UpdateCurrentLanguageFromLanguages();

        Assert.Same(second, sut.CurrentLanguage);
    }

    [Fact]
    public void SetupLanguageSearchHandler_trims_term_before_invoke()
    {
        var sut = new MusicPublicationSelectionPropertyManager();
        var seen = new List<string?>();
        sut.SetupLanguageSearchHandler(term =>
        {
            seen.Add(term);
            return Task.CompletedTask;
        });

        sut.LanguageSearchTerm = "  hello  ";

        Assert.Single(seen);
        Assert.Equal("hello", seen[0]);
    }

    [Fact]
    public void RemoveLanguageSearchHandler_stops_SearchTerm_feedback()
    {
        var sut = new MusicPublicationSelectionPropertyManager();
        var count = 0;
        sut.SetupLanguageSearchHandler(_ =>
        {
            count++;
            return Task.CompletedTask;
        });

        sut.RemoveLanguageSearchHandler();
        sut.LanguageSearchTerm = "after";

        Assert.Equal(0, count);
    }

    [Fact]
    public void SongPublications_initialized_empty_collection_property()
    {
        var sut = new MusicPublicationSelectionPropertyManager();

        Assert.Empty(sut.SongPublications);
    }

    [Fact]
    public void SelectedItem_alias_points_at_CurrentLanguage_assignment()
    {
        var sut = new MusicPublicationSelectionPropertyManager();

        sut.CurrentLanguage = LangRow("E");

        Assert.Same(sut.CurrentLanguage, sut.SelectedItem as LanguageListViewItemModel);
    }
}
