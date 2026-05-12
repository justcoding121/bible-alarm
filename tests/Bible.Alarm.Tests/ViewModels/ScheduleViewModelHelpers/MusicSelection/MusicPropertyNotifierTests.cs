#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Stores.Models;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class MusicPropertyNotifierTests
{
    private sealed class FakeState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    [Fact]
    public void NotifyFlowDirectionChanged_invokes_property_changed_for_content_flow_direction()
    {
        var names = new List<string>();
        var display = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>())),
            new IdleCatalogMediaService(),
            TestLogging.CreateLogger());

        var sut = new MusicPropertyNotifier(names.Add, display);

        sut.NotifyFlowDirectionChanged();

        Assert.Single(names);
        Assert.Equal(nameof(MusicSelectionContainerViewModel.ContentFlowDirection), names[0]);
    }

    [Fact]
    public void NotifyPropertiesChanged_when_language_changes_requests_scroll_for_vocal_music()
    {
        var names = new List<string>();
        var scrollFlags = new List<bool>();
        var display = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>())),
            new IdleCatalogMediaService(),
            TestLogging.CreateLogger());
        var sut = new MusicPropertyNotifier(names.Add, display);

        sut.NotifyPropertiesChanged(
            languageCodeChanged: true,
            publicationCodeChanged: false,
            sectionCodeChanged: false,
            trackCodeChanged: false,
            repeatChanged: false,
            isMelodyMusic: false,
            setShouldScrollToBottom: scrollFlags.Add);

        Assert.Contains(nameof(MusicSelectionContainerViewModel.ContentFlowDirection), names);
        Assert.Single(scrollFlags);
        Assert.True(scrollFlags[0]);
    }

    [Fact]
    public void NotifyPropertiesChanged_when_language_changes_does_not_request_scroll_for_melody_music()
    {
        var scrollFlags = new List<bool>();
        var display = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>())),
            new IdleCatalogMediaService(),
            TestLogging.CreateLogger());
        var sut = new MusicPropertyNotifier(_ => { }, display);

        sut.NotifyPropertiesChanged(
            languageCodeChanged: true,
            publicationCodeChanged: false,
            sectionCodeChanged: false,
            trackCodeChanged: false,
            repeatChanged: false,
            isMelodyMusic: true,
            setShouldScrollToBottom: scrollFlags.Add);

        Assert.Empty(scrollFlags);
    }

    [Fact]
    public void NotifyPropertiesChanged_when_only_publication_changes_notifies_song_publication_and_dependent_properties()
    {
        var names = new List<string>();
        var display = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>())),
            new IdleCatalogMediaService(),
            TestLogging.CreateLogger());
        var sut = new MusicPropertyNotifier(names.Add, display);

        sut.NotifyPropertiesChanged(
            languageCodeChanged: false,
            publicationCodeChanged: true,
            sectionCodeChanged: false,
            trackCodeChanged: false,
            repeatChanged: false,
            isMelodyMusic: false);

        Assert.Contains(nameof(MusicSelectionContainerViewModel.SongPublicationDisplayText), names);
        Assert.Contains(nameof(MusicSelectionContainerViewModel.IsMusicSectionVisible), names);
        Assert.DoesNotContain(nameof(MusicSelectionContainerViewModel.ContentFlowDirection), names);
    }
}
