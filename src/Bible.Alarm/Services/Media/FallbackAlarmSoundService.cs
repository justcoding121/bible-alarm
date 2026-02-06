#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class FallbackAlarmSoundService(ILogger logger) : IFallbackAlarmSoundService
{

    public async Task<AudioPlayerTrack?> GetFallbackAlarmTrackAsync()
    {
        try
        {
            var fallbackUri = await GetFallbackAlarmSoundUriAsync();
            if (fallbackUri == null)
            {
                logger.Error("Failed to get fallback alarm sound URI");
                return null;
            }

            var fallbackMetadata = new TrackMetadata
            {
                PublicationCode = "Fallback",
                TrackCode = "1",
                // Not from media index; use sentinel so LookUpPath getter is never used for cache/key
                LookUpPath = "?fallback=1"
            };

            return new AudioPlayerTrack
            {
                Uri = fallbackUri,
                PlayItem = new PlayItem(fallbackMetadata, fallbackUri)
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error creating fallback alarm track");
            return null;
        }
    }

    private async Task<string?> GetFallbackAlarmSoundUriAsync()
    {
        // No longer using custom alarm sound file - return null to use platform default
        // The error handling in PlaybackService will display an appropriate message
        logger.Debug("Fallback alarm sound service returning null - using platform default");
        await Task.CompletedTask;
        return null;
    }

}

