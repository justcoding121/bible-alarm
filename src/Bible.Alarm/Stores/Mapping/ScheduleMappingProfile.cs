#nullable enable
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Stores.Mapping;

/// <summary>
/// AutoMapper profile for mapping between database entities and state DTOs.
/// </summary>
public class ScheduleMappingProfile : Profile
{
    public ScheduleMappingProfile()
    {
        // Map AlarmSchedule to ScheduleStateItem (flatten nested entities)
        CreateMap<AlarmSchedule, ScheduleStateItem>()
            .ForMember(dest => dest.BibleReadingScheduleId, opt => opt.MapFrom(src => src.BibleReadingSchedule != null ? (int?)src.BibleReadingSchedule.Id : null))
            .ForMember(dest => dest.BibleReadingLanguageCode, opt => opt.MapFrom(src => src.BibleReadingSchedule != null ? src.BibleReadingSchedule.LanguageCode : null))
            .ForMember(dest => dest.BibleReadingPublicationCode, opt => opt.MapFrom(src => src.BibleReadingSchedule != null ? src.BibleReadingSchedule.PublicationCode : null))
            .ForMember(dest => dest.BibleReadingBookNumber, opt => opt.MapFrom(src => src.BibleReadingSchedule != null ? (int?)src.BibleReadingSchedule.BookNumber : null))
            .ForMember(dest => dest.BibleReadingChapterNumber, opt => opt.MapFrom(src => src.BibleReadingSchedule != null ? (int?)src.BibleReadingSchedule.ChapterNumber : null))
            .ForMember(dest => dest.BibleReadingFinishedDuration, opt => opt.MapFrom(src => src.BibleReadingSchedule != null ? (TimeSpan?)src.BibleReadingSchedule.FinishedDuration : null))
            .ForMember(dest => dest.MusicId, opt => opt.MapFrom(src => src.Music != null ? (int?)src.Music.Id : null))
            .ForMember(dest => dest.MusicType, opt => opt.MapFrom(src => src.Music != null ? (MusicType?)src.Music.MusicType : null))
            .ForMember(dest => dest.MusicPublicationCode, opt => opt.MapFrom(src => src.Music != null ? src.Music.PublicationCode : null))
            .ForMember(dest => dest.MusicLanguageCode, opt => opt.MapFrom(src => src.Music != null ? src.Music.LanguageCode : null))
            .ForMember(dest => dest.MusicTrackNumber, opt => opt.MapFrom(src => src.Music != null ? (int?)src.Music.TrackNumber : null))
            .ForMember(dest => dest.MusicRepeat, opt => opt.MapFrom(src => src.Music != null ? (bool?)src.Music.Repeat : null))
            .ForMember(dest => dest.TranslationName, opt => opt.Ignore()); // Set manually during bootstrap
        
        // Map ScheduleStateItem back to AlarmSchedule (for when we need the entity)
        // Note: This creates a new AlarmSchedule but won't have EF tracking
        CreateMap<ScheduleStateItem, AlarmSchedule>()
            .ForMember(dest => dest.BibleReadingSchedule, opt => opt.MapFrom(src => src.BibleReadingScheduleId.HasValue ? new BibleReadingSchedule
            {
                Id = src.BibleReadingScheduleId.Value,
                LanguageCode = src.BibleReadingLanguageCode ?? string.Empty,
                PublicationCode = src.BibleReadingPublicationCode ?? string.Empty,
                BookNumber = src.BibleReadingBookNumber ?? 0,
                ChapterNumber = src.BibleReadingChapterNumber ?? 0,
                FinishedDuration = src.BibleReadingFinishedDuration ?? TimeSpan.Zero,
                AlarmScheduleId = src.Id
            } : null))
            .ForMember(dest => dest.Music, opt => opt.MapFrom(src => src.MusicId.HasValue ? new AlarmMusic
            {
                Id = src.MusicId.Value,
                MusicType = src.MusicType ?? MusicType.Melodies,
                PublicationCode = src.MusicPublicationCode ?? string.Empty,
                LanguageCode = src.MusicLanguageCode,
                TrackNumber = src.MusicTrackNumber ?? 0,
                Repeat = src.MusicRepeat ?? false,
                AlarmScheduleId = src.Id
            } : null))
            .ForMember(dest => dest.AlarmNotifications, opt => opt.Ignore()) // Not stored in state
            .ForMember(dest => dest.CronExpression, opt => opt.Ignore()) // Computed property
            .ForMember(dest => dest.NextFireDate, opt => opt.Ignore()) // Method, not property
            .ForMember(dest => dest.MeridianHour, opt => opt.Ignore()) // Computed property
            .ForMember(dest => dest.Meridian, opt => opt.Ignore()) // Computed property
            .ForMember(dest => dest.TimeText, opt => opt.Ignore()); // Computed property
    }
}

