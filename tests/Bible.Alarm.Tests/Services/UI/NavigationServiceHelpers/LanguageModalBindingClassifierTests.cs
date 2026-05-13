#nullable enable

using Bible.Alarm.Services.UI.NavigationServiceHelpers;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music;

namespace Bible.Alarm.Tests;

public sealed class LanguageModalBindingClassifierTests
{
    [Fact]
    public void ClassifyBindingContextRuntimeType_maps_bible_publication_selection_view_model_to_bible_modal()
    {
        Assert.Equal(
            LanguageModalKind.Bible,
            LanguageModalBindingClassifier.ClassifyBindingContextRuntimeType(typeof(BiblePublicationSelectionViewModel)));
    }

    [Fact]
    public void ClassifyBindingContextRuntimeType_maps_music_publication_selection_view_model_to_music_modal()
    {
        Assert.Equal(
            LanguageModalKind.Music,
            LanguageModalBindingClassifier.ClassifyBindingContextRuntimeType(typeof(MusicPublicationSelectionViewModel)));
    }

    [Fact]
    public void ClassifyBindingContextRuntimeType_throws_when_binding_context_type_not_supported()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            LanguageModalBindingClassifier.ClassifyBindingContextRuntimeType(typeof(string)));

        Assert.Equal("bindingContextType", ex.ParamName);
    }
}
