#nullable enable
using AutoMapper;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
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
            .ForMember(dest => dest.BiblePublicationScheduleId, opt => opt.MapFrom(src => src.BiblePublicationSchedule != null ? (int?)src.BiblePublicationSchedule.Id : null))
            .ForMember(dest => dest.BiblePublicationLanguageCode, opt => opt.MapFrom(src => src.BiblePublicationSchedule != null ? src.BiblePublicationSchedule.LanguageCode : null))
            .ForMember(dest => dest.BiblePublicationCode, opt => opt.MapFrom(src => src.BiblePublicationSchedule != null ? src.BiblePublicationSchedule.PublicationCode : null))
            .ForMember(dest => dest.BiblePublicationSectionNumber, opt => opt.MapFrom(src => 
                src.BiblePublicationSchedule != null ? ConvertSectionCodeToInt(src.BiblePublicationSchedule.SectionCode) : null))
            .ForMember(dest => dest.BiblePublicationTrackNumber, opt => opt.MapFrom(src => src.BiblePublicationSchedule != null ? (int?)src.BiblePublicationSchedule.TrackNumber : null))
            .ForMember(dest => dest.BiblePublicationFinishedDuration, opt => opt.MapFrom(src => src.BiblePublicationSchedule != null ? (TimeSpan?)src.BiblePublicationSchedule.FinishedDuration : null))
            .ForMember(dest => dest.MusicId, opt => opt.MapFrom(src => src.Music != null ? (int?)src.Music.Id : null))
            .ForMember(dest => dest.MusicType, opt => opt.MapFrom(src => src.Music != null ? (MusicType?)src.Music.MusicType : null))
            .ForMember(dest => dest.MusicPublicationCode, opt => opt.MapFrom(src => src.Music != null ? src.Music.PublicationCode : null))
            .ForMember(dest => dest.MusicLanguageCode, opt => opt.MapFrom(src => src.Music != null ? src.Music.LanguageCode : null))
            .ForMember(dest => dest.MusicTrackNumber, opt => opt.MapFrom(src => src.Music != null ? (int?)src.Music.TrackNumber : null))
            .ForMember(dest => dest.MusicRepeat, opt => opt.MapFrom(src => src.Music != null ? (bool?)src.Music.Repeat : null))
            .ForMember(dest => dest.BiblePublicationLanguageName, opt => opt.Ignore()) // Set manually during bootstrap
            .ForMember(dest => dest.BiblePublicationSectionName, opt => opt.Ignore()); // Set manually during bootstrap

        // Map ScheduleStateItem back to AlarmSchedule (for when we need the entity)
        // Note: This creates a new AlarmSchedule but won't have EF tracking
        CreateMap<ScheduleStateItem, AlarmSchedule>()
            .ForMember(dest => dest.BiblePublicationSchedule, opt => opt.MapFrom(src => src.BiblePublicationScheduleId.HasValue ? new BiblePublicationSchedule
            {
                Id = src.BiblePublicationScheduleId.Value,
                LanguageCode = src.BiblePublicationLanguageCode ?? string.Empty,
                PublicationCode = src.BiblePublicationCode ?? string.Empty,
                SectionCode = src.BiblePublicationSectionNumber.HasValue ? src.BiblePublicationSectionNumber.Value.ToString() : null,
                TrackNumber = src.BiblePublicationTrackNumber ?? 0,
                FinishedDuration = src.BiblePublicationFinishedDuration ?? TimeSpan.Zero,
                AlarmScheduleId = src.Id
            } : null))
            .ForMember(dest => dest.Music, opt => opt.MapFrom(src => (src.MusicId.HasValue || src.MusicType.HasValue) ? new AlarmMusic
            {
                Id = src.MusicId ?? 0,
                MusicType = src.MusicType ?? MusicType.Music,
                PublicationCode = src.MusicPublicationCode ?? string.Empty,
                LanguageCode = src.MusicLanguageCode,
                TrackNumber = src.MusicTrackNumber ?? 0,
                Repeat = src.MusicRepeat ?? false,
                AlarmScheduleId = src.Id
            } : null))
            .ForMember(dest => dest.AlarmNotifications, opt => opt.Ignore()) // Not stored in state
            .ForMember(dest => dest.CronExpression, opt => opt.Ignore()) // Computed property
            .ForMember(dest => dest.MeridianHour, opt => opt.Ignore()) // Computed property
            .ForMember(dest => dest.Meridian, opt => opt.Ignore()) // Computed property
            .ForMember(dest => dest.TimeText, opt => opt.Ignore()); // Computed property

        // Map AlarmMusic to MusicStateItem
        CreateMap<AlarmMusic, MusicStateItem>();

        // Map MusicStateItem back to AlarmMusic
        CreateMap<MusicStateItem, AlarmMusic>()
            .ForMember(dest => dest.AlarmSchedule, opt => opt.Ignore()); // Not stored in state

        // Map BiblePublicationSchedule to BiblePublicationStateItem
        CreateMap<BiblePublicationSchedule, BiblePublicationStateItem>();

        // Map BiblePublicationStateItem back to BiblePublicationSchedule
        CreateMap<BiblePublicationStateItem, BiblePublicationSchedule>()
            .ForMember(dest => dest.AlarmSchedule, opt => opt.Ignore()); // Not stored in state
    }

    /// <summary>
    /// Helper method to convert SectionCode (string) to int? for state DTOs.
    /// Tries to parse SectionCode to int. Returns null if parsing fails or SectionCode is null/empty.
    /// </summary>
    private static int? ConvertSectionCodeToInt(string? sectionCode)
    {
        if (string.IsNullOrEmpty(sectionCode))
        {
            return null;
        }

        if (int.TryParse(sectionCode, out var sectionNum))
        {
            return sectionNum;
        }

        return null;
    }
}

