#nullable enable
using AutoMapper;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Shared.Helpers;

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
            .ForMember(dest => dest.BiblePublicationScheduleId, opt => opt.MapFrom(src => src.BiblePublicationSchedule != null ? (int?)src.BiblePublicationSchedule.Id : null))
            .ForMember(dest => dest.BiblePublicationLanguageCode, opt => opt.MapFrom(src => src.BiblePublicationSchedule != null ? src.BiblePublicationSchedule.LanguageCode : null))
            .ForMember(dest => dest.BiblePublicationCode, opt => opt.MapFrom(src => src.BiblePublicationSchedule != null ? src.BiblePublicationSchedule.PublicationCode : null))
            .ForMember(dest => dest.BiblePublicationSectionCode, opt => opt.MapFrom(src =>
                src.BiblePublicationSchedule != null ? SectionCodeHelper.Normalize(src.BiblePublicationSchedule.SectionCode) : null))
            .ForMember(dest => dest.BiblePublicationTrackCode, opt => opt.MapFrom(src => src.BiblePublicationSchedule != null ? src.BiblePublicationSchedule.TrackCode : null))
            .ForMember(dest => dest.BiblePublicationFinishedDuration, opt => opt.MapFrom(src => src.BiblePublicationSchedule != null ? (TimeSpan?)src.BiblePublicationSchedule.FinishedDuration : null))
            .ForMember(dest => dest.MusicId, opt => opt.MapFrom(src => src.Music != null ? (int?)src.Music.Id : null))
            .ForMember(dest => dest.MusicPublicationCode, opt => opt.MapFrom(src => src.Music != null ? src.Music.PublicationCode : null))
            .ForMember(dest => dest.MusicLanguageCode, opt => opt.MapFrom(src => src.Music != null ? src.Music.LanguageCode : null))
            .ForMember(dest => dest.MusicSectionCode, opt => opt.MapFrom(src => src.Music != null ? src.Music.SectionCode : null))
            .ForMember(dest => dest.MusicTrackCode, opt => opt.MapFrom(src => src.Music != null ? src.Music.TrackCode : null))
            .ForMember(dest => dest.MusicRepeat, opt => opt.MapFrom(src => src.Music != null ? (bool?)src.Music.Repeat : null))
            // Set manually during bootstrap
            .ForMember(dest => dest.BiblePublicationLanguageName, opt => opt.Ignore())
            // Set manually during bootstrap
            .ForMember(dest => dest.BiblePublicationSectionName, opt => opt.Ignore());

        // Map ScheduleStateItem back to AlarmSchedule (for when we need the entity)
        // Note: This creates a new AlarmSchedule but won't have EF tracking
        CreateMap<ScheduleStateItem, AlarmSchedule>()
            .ForMember(dest => dest.BiblePublicationSchedule, opt => opt.MapFrom(src => src.BiblePublicationScheduleId.HasValue ? new BiblePublicationSchedule
            {
                Id = src.BiblePublicationScheduleId.Value,
                LanguageCode = src.BiblePublicationLanguageCode ?? string.Empty,
                PublicationCode = src.BiblePublicationCode ?? string.Empty,
                SectionCode = SectionCodeHelper.Normalize(src.BiblePublicationSectionCode),
                TrackCode = src.BiblePublicationTrackCode ?? string.Empty,
                FinishedDuration = src.BiblePublicationFinishedDuration ?? TimeSpan.Zero,
                AlarmScheduleId = src.Id
            } : null))
            .ForMember(dest => dest.Music, opt => opt.MapFrom(src => (src.MusicId.HasValue || !string.IsNullOrEmpty(src.MusicPublicationCode)) ? new AlarmMusic
            {
                Id = src.MusicId ?? 0,
                PublicationCode = src.MusicPublicationCode ?? string.Empty,
                LanguageCode = src.MusicLanguageCode,
                SectionCode = src.MusicSectionCode,
                TrackCode = src.MusicTrackCode ?? string.Empty,
                Repeat = src.MusicRepeat ?? false,
                AlarmScheduleId = src.Id
            } : null))
            // Not stored in state
            .ForMember(dest => dest.AlarmNotifications, opt => opt.Ignore())
            // Computed property
            .ForMember(dest => dest.CronExpression, opt => opt.Ignore())
            // Computed property
            .ForMember(dest => dest.MeridianHour, opt => opt.Ignore())
            // Computed property
            .ForMember(dest => dest.Meridian, opt => opt.Ignore())
            // Computed property
            .ForMember(dest => dest.TimeText, opt => opt.Ignore());

        // Map AlarmMusic to MusicStateItem
        CreateMap<AlarmMusic, MusicStateItem>();

        // Map MusicStateItem back to AlarmMusic
        CreateMap<MusicStateItem, AlarmMusic>()
            // Not stored in state
            .ForMember(dest => dest.AlarmSchedule, opt => opt.Ignore());

        // Map BiblePublicationSchedule to BiblePublicationStateItem
        CreateMap<BiblePublicationSchedule, BiblePublicationStateItem>();

        // Map BiblePublicationStateItem back to BiblePublicationSchedule
        CreateMap<BiblePublicationStateItem, BiblePublicationSchedule>()
            // Not stored in state
            .ForMember(dest => dest.AlarmSchedule, opt => opt.Ignore());
    }

}

