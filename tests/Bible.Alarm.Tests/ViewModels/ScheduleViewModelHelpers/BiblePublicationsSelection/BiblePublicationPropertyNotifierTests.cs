#nullable enable

using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;
using PropertyChangeInfo = Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection.BiblePublicationPropertyChangeDetector.PropertyChangeInfo;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationPropertyNotifierTests
{
    [Fact]
    public void Ctor_throws_when_callback_null()
    {
        Assert.Throws<ArgumentNullException>(() => new BiblePublicationPropertyNotifier(null!));
    }

    [Fact]
    public void NotifyAllDisplayTextPropertiesChanged_invokes_expected_property_names()
    {
        var names = new List<string>();
        var sut = new BiblePublicationPropertyNotifier(names.Add);

        sut.NotifyAllDisplayTextPropertiesChanged();

        Assert.Equal(
            [
                nameof(BiblePublicationSelectionContainerViewModel.CategoryDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.IsLanguageVisible),
                nameof(BiblePublicationSelectionContainerViewModel.IsSectionVisible),
                nameof(BiblePublicationSelectionContainerViewModel.ContentFlowDirection),
                nameof(BiblePublicationSelectionContainerViewModel.LanguageDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.PublicationDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText),
            ],
            names);
    }

    [Fact]
    public void NotifyPropertyChanges_category_branch_covers_full_cascade()
    {
        var names = new List<string>();
        var sut = new BiblePublicationPropertyNotifier(names.Add);

        sut.NotifyPropertyChanges(new PropertyChangeInfo { NotifyCategory = true });

        Assert.Equal(
            [
                nameof(BiblePublicationSelectionContainerViewModel.CategoryDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.IsLanguageVisible),
                nameof(BiblePublicationSelectionContainerViewModel.ContentFlowDirection),
                nameof(BiblePublicationSelectionContainerViewModel.LanguageDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.PublicationDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.IsSectionVisible),
                nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText),
            ],
            names);
    }

    [Fact]
    public void NotifyPropertyChanges_language_branch_skips_category()
    {
        var names = new List<string>();
        var sut = new BiblePublicationPropertyNotifier(names.Add);

        sut.NotifyPropertyChanges(new PropertyChangeInfo { NotifyLanguage = true });

        Assert.Equal(
            [
                nameof(BiblePublicationSelectionContainerViewModel.IsLanguageVisible),
                nameof(BiblePublicationSelectionContainerViewModel.ContentFlowDirection),
                nameof(BiblePublicationSelectionContainerViewModel.LanguageDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.PublicationDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.IsSectionVisible),
                nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText),
            ],
            names);
    }

    [Fact]
    public void NotifyPropertyChanges_publication_branch()
    {
        var names = new List<string>();
        var sut = new BiblePublicationPropertyNotifier(names.Add);

        sut.NotifyPropertyChanges(new PropertyChangeInfo { NotifyPublication = true });

        Assert.Equal(
            [
                nameof(BiblePublicationSelectionContainerViewModel.PublicationDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.IsSectionVisible),
                nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText),
            ],
            names);
    }

    [Fact]
    public void NotifyPropertyChanges_section_branch()
    {
        var names = new List<string>();
        var sut = new BiblePublicationPropertyNotifier(names.Add);

        sut.NotifyPropertyChanges(new PropertyChangeInfo { NotifySection = true });

        Assert.Equal(
            [
                nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText),
                nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText),
            ],
            names);
    }

    [Fact]
    public void NotifyPropertyChanges_track_branch()
    {
        var names = new List<string>();
        var sut = new BiblePublicationPropertyNotifier(names.Add);

        sut.NotifyPropertyChanges(new PropertyChangeInfo { NotifyTrack = true });

        Assert.Equal(
            [nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText)],
            names);
    }

    [Fact]
    public void NotifyPropertyChanges_display_text_only_category()
    {
        var names = new List<string>();
        var sut = new BiblePublicationPropertyNotifier(names.Add);

        sut.NotifyPropertyChanges(new PropertyChangeInfo
        {
            DisplayTextOnlyChanged = true,
            CategoryDisplayChanged = true,
        });

        Assert.Equal(
            [nameof(BiblePublicationSelectionContainerViewModel.CategoryDisplayText)],
            names);
    }

    [Fact]
    public void NotifyPropertyChanges_is_section_visible_only_when_not_in_cascade()
    {
        var names = new List<string>();
        var sut = new BiblePublicationPropertyNotifier(names.Add);

        sut.NotifyPropertyChanges(new PropertyChangeInfo { NotifyIsSectionVisible = true });

        Assert.Equal(
            [nameof(BiblePublicationSelectionContainerViewModel.IsSectionVisible)],
            names);
    }

    [Fact]
    public void NotifyPropertyChanges_is_language_visible_only_when_not_in_category_or_language_cascade()
    {
        var names = new List<string>();
        var sut = new BiblePublicationPropertyNotifier(names.Add);

        sut.NotifyPropertyChanges(new PropertyChangeInfo { NotifyIsLanguageVisible = true });

        Assert.Equal(
            [nameof(BiblePublicationSelectionContainerViewModel.IsLanguageVisible)],
            names);
    }

    [Fact]
    public void NotifyPropertyChanges_display_text_only_language_publication_section_track()
    {
        var names = new List<string>();
        var sut = new BiblePublicationPropertyNotifier(names.Add);

        sut.NotifyPropertyChanges(new PropertyChangeInfo
        {
            DisplayTextOnlyChanged = true,
            LanguageDisplayChanged = true,
        });
        Assert.Equal([nameof(BiblePublicationSelectionContainerViewModel.LanguageDisplayText)], names);

        names.Clear();
        sut.NotifyPropertyChanges(new PropertyChangeInfo
        {
            DisplayTextOnlyChanged = true,
            PublicationDisplayChanged = true,
        });
        Assert.Equal([nameof(BiblePublicationSelectionContainerViewModel.PublicationDisplayText)], names);

        names.Clear();
        sut.NotifyPropertyChanges(new PropertyChangeInfo
        {
            DisplayTextOnlyChanged = true,
            SectionDisplayChanged = true,
        });
        Assert.Equal([nameof(BiblePublicationSelectionContainerViewModel.SectionDisplayText)], names);

        names.Clear();
        sut.NotifyPropertyChanges(new PropertyChangeInfo
        {
            DisplayTextOnlyChanged = true,
            TrackDisplayChanged = true,
        });
        Assert.Equal([nameof(BiblePublicationSelectionContainerViewModel.TrackDisplayText)], names);
    }
}
