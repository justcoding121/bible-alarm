#nullable enable

using System.ComponentModel;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Xunit;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionPropertyForwarderTests
{
    [Fact]
    public void CreateHandler_IsBusy_notifies_IsBusy_and_ShowCancelButton()
    {
        var notified = new List<string>();
        var handler = MusicPublicationSelectionPropertyForwarder.CreateHandler(notified.Add);

        handler(null, new PropertyChangedEventArgs(nameof(MusicPublicationSelectionPropertyManager.IsBusy)));

        Assert.Equal(new[] { "IsBusy", "ShowCancelButton" }, notified);
    }

    [Fact]
    public void CreateHandler_ShowProgress_notifies_ShowProgress_and_ShowCancelButton()
    {
        var notified = new List<string>();
        var handler = MusicPublicationSelectionPropertyForwarder.CreateHandler(notified.Add);

        handler(null, new PropertyChangedEventArgs(nameof(MusicPublicationSelectionPropertyManager.ShowProgress)));

        Assert.Equal(new[] { "ShowProgress", "ShowCancelButton" }, notified);
    }

    [Fact]
    public void CreateHandler_HasFetchError_notifies_HasFetchError_and_ShowCancelButton()
    {
        var notified = new List<string>();
        var handler = MusicPublicationSelectionPropertyForwarder.CreateHandler(notified.Add);

        handler(null, new PropertyChangedEventArgs(nameof(MusicPublicationSelectionPropertyManager.HasFetchError)));

        Assert.Equal(new[] { "HasFetchError", "ShowCancelButton" }, notified);
    }

    [Fact]
    public void CreateHandler_ProgressPercent_only_notifies_ProgressPercent()
    {
        var notified = new List<string>();
        var handler = MusicPublicationSelectionPropertyForwarder.CreateHandler(notified.Add);

        handler(null, new PropertyChangedEventArgs(nameof(MusicPublicationSelectionPropertyManager.ProgressPercent)));

        Assert.Equal(new[] { "ProgressPercent" }, notified);
    }

    [Fact]
    public void CreateHandler_ProgressText_CanCancelFetch_and_IsCancelBusy_notify_matching_vm_properties()
    {
        var notified = new List<string>();
        var handler = MusicPublicationSelectionPropertyForwarder.CreateHandler(notified.Add);

        handler(null, new PropertyChangedEventArgs(nameof(MusicPublicationSelectionPropertyManager.ProgressText)));
        Assert.Equal(new[] { "ProgressText" }, notified);

        notified.Clear();
        handler(null, new PropertyChangedEventArgs(nameof(MusicPublicationSelectionPropertyManager.CanCancelFetch)));
        Assert.Equal(new[] { "CanCancelFetch" }, notified);

        notified.Clear();
        handler(null, new PropertyChangedEventArgs(nameof(MusicPublicationSelectionPropertyManager.IsCancelBusy)));
        Assert.Equal(new[] { "IsCancelBusy" }, notified);
    }

    [Fact]
    public void CreateHandler_unknown_property_does_not_notify()
    {
        var notified = new List<string>();
        var handler = MusicPublicationSelectionPropertyForwarder.CreateHandler(notified.Add);

        handler(null, new PropertyChangedEventArgs(nameof(MusicPublicationSelectionPropertyManager.LanguageSearchTerm)));

        Assert.Empty(notified);
    }
}
