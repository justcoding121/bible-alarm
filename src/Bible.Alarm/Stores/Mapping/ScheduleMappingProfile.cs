#nullable enable
using AutoMapper;
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
        ConfigureAlarmScheduleToScheduleStateItem();
        ConfigureScheduleStateItemToAlarmSchedule();
        ConfigureAlarmMusicMaps();
        ConfigureBiblePublicationMaps();
        ConfigureScheduleStateItemClone();
    }

    private void ConfigureAlarmScheduleToScheduleStateItem()
    {
        CreateMap<AlarmSchedule, ScheduleStateItem>()
            .ForMember(dest => dest.BiblePublicationScheduleId, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.BiblePublicationScheduleId(src)))
            .ForMember(dest => dest.BiblePublicationLanguageCode, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.BiblePublicationLanguageCode(src)))
            .ForMember(dest => dest.BiblePublicationCode, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.BiblePublicationCode(src)))
            .ForMember(dest => dest.BiblePublicationSectionCode, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.BiblePublicationSectionCode(src)))
            .ForMember(dest => dest.BiblePublicationTrackCode, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.BiblePublicationTrackCode(src)))
            .ForMember(dest => dest.BiblePublicationFinishedDuration, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.BiblePublicationFinishedDuration(src)))
            .ForMember(dest => dest.MusicId, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.MusicId(src)))
            .ForMember(dest => dest.MusicPublicationCode, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.MusicPublicationCode(src)))
            .ForMember(dest => dest.MusicLanguageCode, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.MusicLanguageCode(src)))
            .ForMember(dest => dest.MusicSectionCode, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.MusicSectionCode(src)))
            .ForMember(dest => dest.MusicTrackCode, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.MusicTrackCode(src)))
            .ForMember(dest => dest.MusicRepeat, opt => opt.MapFrom((AlarmSchedule src) => AlarmScheduleToScheduleStateItemMap.MusicRepeat(src)))
            .ForMember(dest => dest.BiblePublicationCategoryName, opt => opt.MapFrom(src => src.CategoryCode))
            .ForMember(dest => dest.BiblePublicationLanguageName, opt => opt.Ignore())
            .ForMember(dest => dest.BiblePublicationSectionName, opt => opt.Ignore());
    }

    private void ConfigureScheduleStateItemToAlarmSchedule()
    {
        CreateMap<ScheduleStateItem, AlarmSchedule>()
            .ForMember(dest => dest.BiblePublicationSchedule, opt => opt.MapFrom((ScheduleStateItem src) => ScheduleStateItemToAlarmScheduleMap.BiblePublicationSchedule(src)))
            .ForMember(dest => dest.Music, opt => opt.MapFrom((ScheduleStateItem src) => ScheduleStateItemToAlarmScheduleMap.Music(src)))
            .ForMember(dest => dest.AlarmNotifications, opt => opt.Ignore())
            .ForMember(dest => dest.CronExpression, opt => opt.Ignore())
            .ForMember(dest => dest.MeridianHour, opt => opt.Ignore())
            .ForMember(dest => dest.Meridian, opt => opt.Ignore())
            .ForMember(dest => dest.TimeText, opt => opt.Ignore())
            .ForMember(dest => dest.CategoryCode, opt => opt.Ignore());
    }

    private void ConfigureAlarmMusicMaps()
    {
        CreateMap<AlarmMusic, MusicStateItem>();
        CreateMap<MusicStateItem, AlarmMusic>()
            .ForMember(dest => dest.AlarmSchedule, opt => opt.Ignore());
    }

    private void ConfigureBiblePublicationMaps()
    {
        CreateMap<BiblePublicationSchedule, BiblePublicationStateItem>();
        CreateMap<BiblePublicationStateItem, BiblePublicationSchedule>()
            .ForMember(dest => dest.AlarmSchedule, opt => opt.Ignore());
    }

    private void ConfigureScheduleStateItemClone()
    {
        CreateMap<ScheduleStateItem, ScheduleStateItem>();
    }
}
