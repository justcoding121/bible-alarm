#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.Maui.Storage;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class FallbackAlarmSoundService(ILogger logger) : IFallbackAlarmSoundService
{
    private readonly ILogger _logger = logger;

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
        try
        {
            const string resourceFileName = "cool_alarm_tone_notification_sound.mp3";
            var cacheDir = FileSystem.CacheDirectory;
            var cachedFilePath = Path.Combine(cacheDir, resourceFileName);

            // Check if already cached
            if (File.Exists(cachedFilePath))
            {
                return new Uri(cachedFilePath).AbsoluteUri;
            }

            // Copy from app package to cache
            using var stream = await FileSystem.OpenAppPackageFileAsync(resourceFileName);
            if (stream == null)
            {
                _logger.Error($"Failed to open app package file: {resourceFileName}");
                return null;
            }

            using var fileStream = File.Create(cachedFilePath);
            await stream.CopyToAsync(fileStream);

            return new Uri(cachedFilePath).AbsoluteUri;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting fallback alarm sound URI");
            return null;
        }
    }
}

