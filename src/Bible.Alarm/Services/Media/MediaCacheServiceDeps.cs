#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed record MediaCacheServiceDeps(
    ILogger Logger,
    IStorageService StorageService,
    IDownloadService DownloadService,
    IPlaylistService MediaPlayService,
    IMediaService MediaService,
    INetworkStatusService NetworkStatusService,
    IMediaUrlRefreshService UrlRefreshService,
    IAlarmScheduleService AlarmScheduleService,
    ITrackCdnUrlRefresher TrackCdnUrlRefresher);
