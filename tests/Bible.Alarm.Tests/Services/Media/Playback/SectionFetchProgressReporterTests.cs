#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Playback;
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.Tests;

public sealed class SectionFetchProgressReporterTests
{
    internal sealed class RecordingRecipient : IRecipient<PlaybackPreparationProgressMessage>, IDisposable
    {
        public double? LastCurrentTrackProgress { get; private set; }

        public RecordingRecipient() =>
            WeakReferenceMessenger.Default.Register<PlaybackPreparationProgressMessage>(this);

        public void Receive(PlaybackPreparationProgressMessage message) =>
            LastCurrentTrackProgress = message.CurrentTrackProgress;

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<PlaybackPreparationProgressMessage>(this);
    }

    [Fact]
    public void UpdateProgress_clamps_negative_progress_for_preparation_message()
    {
        using var recipient = new RecordingRecipient();
        using var cts = new CancellationTokenSource();
        var sut = new SectionFetchProgressReporter(cts.Token);

        sut.UpdateProgress(-0.25);

        Assert.Equal(0.0, recipient.LastCurrentTrackProgress);
    }

    [Fact]
    public void UpdateProgress_clamps_progress_above_one_for_preparation_message()
    {
        using var recipient = new RecordingRecipient();
        var sut = new SectionFetchProgressReporter(default);

        sut.UpdateProgress(4);

        Assert.Equal(1.0, recipient.LastCurrentTrackProgress);
    }

    [Fact]
    public void Ctor_assigns_cancellation_token_from_argument()
    {
        using var cts = new CancellationTokenSource();
        var sut = new SectionFetchProgressReporter(cts.Token);

        Assert.Equal(cts.Token, sut.CancellationToken);
    }
}
