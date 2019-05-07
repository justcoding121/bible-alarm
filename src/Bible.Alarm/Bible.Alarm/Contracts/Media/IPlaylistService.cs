﻿using JW.Alarm.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JW.Alarm.Services.Contracts
{
    public interface IPlaylistService
    {
        Task<PlayItem> NextTrack(long scheduleId);
        Task<PlayItem> NextTrack(NotificationDetail trackDetail);
        Task MarkTrackAsPlayed(NotificationDetail trackDetail);
        Task MarkTrackAsFinished(NotificationDetail trackDetail);
        Task<List<PlayItem>> NextTracks(long scheduleId, TimeSpan duration);
    }
}
