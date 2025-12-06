#nullable enable
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IDisplayMetadataService : IDisposable
{
    Task<MetaData> GetDisplayMetadataAsync(AudioPlayerTrack track);
}

