#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Stores.Messages.ModalOverlay;
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.Tests;

public sealed class ModalOverlayFetchProgressReporterTests
{
    internal sealed class RecordingRecipient : IRecipient<ModalOverlayFetchProgressMessage>, IDisposable
    {
        public ModalOverlayFetchProgress? Last { get; private set; }

        public RecordingRecipient() =>
            WeakReferenceMessenger.Default.Register<ModalOverlayFetchProgressMessage>(this);

        public void Receive(ModalOverlayFetchProgressMessage message) =>
            Last = message.Value;

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<ModalOverlayFetchProgressMessage>(this);
    }

    internal sealed class ThrowingSubscriber : IRecipient<ModalOverlayFetchProgressMessage>, IDisposable
    {
        public ThrowingSubscriber() =>
            WeakReferenceMessenger.Default.Register<ModalOverlayFetchProgressMessage>(this);

        public void Receive(ModalOverlayFetchProgressMessage message) =>
            throw new InvalidOperationException("subscriber");

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<ModalOverlayFetchProgressMessage>(this);
    }

    [Fact]
    public void UpdateProgress_rounds_percent_for_progress_text()
    {
        using var recipient = new RecordingRecipient();
        var sut = new ModalOverlayFetchProgressReporter("BiblePublication", default);

        sut.UpdateProgress(0.456);

        Assert.Equal("46%", recipient.Last?.ProgressText);
    }

    [Fact]
    public void SetIsVisible_false_sends_empty_progress_text_and_hides_overlay()
    {
        using var recipient = new RecordingRecipient();
        var sut = new ModalOverlayFetchProgressReporter("BibleSection", default);

        sut.SetIsVisible(false);

        Assert.True(recipient.Last is { IsVisible: false, ProgressText: "" });
    }

    [Fact]
    public void UpdateProgressText_swallows_exceptions_from_modal_overlay_subscribers()
    {
        using var _ = new ThrowingSubscriber();
        var sut = new ModalOverlayFetchProgressReporter("BibleSection", default);

        var ex = Record.Exception(() => sut.UpdateProgressText("loading"));

        Assert.Null(ex);
    }

    [Fact]
    public void SetIsVisible_true_sends_zero_percent_progress_text()
    {
        using var recipient = new RecordingRecipient();
        var sut = new ModalOverlayFetchProgressReporter("BiblePublication", default);

        sut.SetIsVisible(true);

        Assert.True(recipient.Last is { IsVisible: true, ProgressText: "0%" });
    }

    [Fact]
    public void Reporters_swallow_exceptions_from_modal_overlay_subscribers()
    {
        using var _ = new ThrowingSubscriber();
        var sut = new ModalOverlayFetchProgressReporter("BiblePublication", default);

        var ex = Record.Exception(() =>
        {
            sut.UpdateProgress(0.1);
            sut.UpdateProgressText("x");
            sut.SetIsVisible(false);
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Ctor_assigns_cancellation_token_from_argument()
    {
        using var cts = new CancellationTokenSource();
        var sut = new ModalOverlayFetchProgressReporter("T", cts.Token);

        Assert.Equal(cts.Token, sut.CancellationToken);
    }
}
