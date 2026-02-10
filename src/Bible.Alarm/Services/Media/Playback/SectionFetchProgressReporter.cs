#nullable enable

using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.Services.Media.Playback;

/// <summary>
/// Sends section fetch progress (0.0 to 1.0) for API metadata fetch phase during playback preparation.
/// </summary>
public sealed class SectionFetchProgressReporter : IFetchProgress
{
    public CancellationToken CancellationToken { get; }

    public SectionFetchProgressReporter(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }

    public void UpdateProgress(double progress)
    {
        var clampedProgress = Math.Max(0.0, Math.Min(1.0, progress));
        WeakReferenceMessenger.Default.Send(new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 0,
            TotalTracks = 1,
            CurrentTrackProgress = clampedProgress,
            BytesDownloaded = 0,
            TotalBytes = null,
            TotalBytesDownloaded = 0,
            TotalBytesExpected = null
        });
    }

    public void UpdateProgressText(string _) { }

    public void SetIsVisible(bool _) { }
}
