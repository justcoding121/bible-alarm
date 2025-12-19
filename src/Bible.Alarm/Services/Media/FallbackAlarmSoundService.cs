#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class FallbackAlarmSoundService(ILogger logger) : IFallbackAlarmSoundService, IDisposable
{
    private readonly ILogger _logger = logger;
    private bool _isDisposed;

    public async Task<AudioPlayerTrack?> GetFallbackAlarmTrackAsync()
    {
        try
        {
            var fallbackUri = await GetFallbackAlarmSoundUriAsync();
            if (fallbackUri == null)
            {
                _logger.Error("Failed to get fallback alarm sound URI");
                return null;
            }

            var fallbackMetadata = new TrackMetadata
            {
                PublicationCode = "Fallback",
                // Set TrackNumber > 0 to make it Music type
                TrackNumber = 1
            };

            return new AudioPlayerTrack
            {
                Uri = fallbackUri,
                PlayItem = new PlayItem(fallbackMetadata, fallbackUri)
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating fallback alarm track");
            return null;
        }
    }

    private async Task<string?> GetFallbackAlarmSoundUriAsync()
    {
        // No longer using custom alarm sound file - return null to use platform default
        // The error handling in PlaybackService will display an appropriate message
        _logger.Debug("Fallback alarm sound service returning null - using platform default");
        await Task.CompletedTask;
        return null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // No event handlers to unsubscribe, no injected services to dispose (logger is singleton)
    }
}

