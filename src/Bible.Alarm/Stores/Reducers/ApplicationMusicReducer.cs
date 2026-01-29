#nullable enable

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers.Services;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Stores.Reducers;

public static class ApplicationMusicReducer
{
    [ReducerMethod]
    public static ApplicationState OnMusicSelection(ApplicationState state, MusicSelectionAction action)
    {
        // Music selection updates CurrentSchedule directly via OnMusicTrackSelected
        // This reducer is kept for backward compatibility but doesn't need to do anything
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnMusicPublicationSelection(ApplicationState state, MusicPublicationSelectionAction action)
    {
        // Song publication selection updates CurrentSchedule directly via OnMusicTrackSelected
        // This reducer is kept for backward compatibility but doesn't need to do anything
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnMusicTrackSelection(ApplicationState state, MusicTrackSelectionAction action)
    {
        // Track selection updates CurrentSchedule directly via OnMusicTrackSelected
        // This reducer is kept for backward compatibility but doesn't need to do anything
        return state;
    }

    [ReducerMethod]
    public static ApplicationState OnMusicTrackSelected(ApplicationState state, TrackSelectedAction action)
    {
        // IMPORTANT: Update CurrentSchedule synchronously here to ensure schedule page shows
        // the new track immediately when modal closes. The async effect runs too late.
        var updatedCurrentSchedule = state.CurrentSchedule;
        if (updatedCurrentSchedule != null && action.CurrentMusic != null)
        {
            var music = action.CurrentMusic;
            updatedCurrentSchedule = updatedCurrentSchedule.DeepClone();
            updatedCurrentSchedule.MusicType = music.MusicType;
            updatedCurrentSchedule.MusicLanguageCode = music.LanguageCode;
            updatedCurrentSchedule.MusicPublicationCode = music.PublicationCode;
            updatedCurrentSchedule.MusicSectionCode = music.SectionCode; // Clear if null (when language/publication changes)
            updatedCurrentSchedule.MusicTrackNumber = music.TrackNumber;
            updatedCurrentSchedule.MusicRepeat = music.Repeat;
            // Also update display names and language direction
            updatedCurrentSchedule.MusicLanguageName = music.LanguageName;
            updatedCurrentSchedule.MusicLanguageDirection = music.LanguageDirection;
            updatedCurrentSchedule.MusicPublicationName = music.PublicationName;
            updatedCurrentSchedule.MusicSectionName = music.SectionName; // Clear if null (when language/publication changes)
            updatedCurrentSchedule.MusicTrackName = music.TrackName;

            Log.Debug("ApplicationMusicReducer.OnMusicTrackSelected: Updated CurrentSchedule with MusicType={MusicType}, TrackNumber={TrackNumber}, TrackName={TrackName}",
                music.MusicType, music.TrackNumber, music.TrackName);
        }

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnMusicSectionSelected(ApplicationState state, MusicSectionSelectedAction action)
    {
        // IMPORTANT: Update CurrentSchedule synchronously here to ensure schedule page shows
        // the new section immediately when modal closes. The async effect runs too late.
        var updatedCurrentSchedule = state.CurrentSchedule;
        if (updatedCurrentSchedule != null && action.CurrentMusic != null)
        {
            var music = action.CurrentMusic;
            updatedCurrentSchedule = updatedCurrentSchedule.DeepClone();
            updatedCurrentSchedule.MusicType = music.MusicType;
            updatedCurrentSchedule.MusicLanguageCode = music.LanguageCode;
            updatedCurrentSchedule.MusicPublicationCode = music.PublicationCode;
            updatedCurrentSchedule.MusicSectionCode = music.SectionCode;
            updatedCurrentSchedule.MusicTrackNumber = music.TrackNumber;
            updatedCurrentSchedule.MusicRepeat = music.Repeat;
            // Also update display names and language direction
            updatedCurrentSchedule.MusicLanguageName = music.LanguageName;
            updatedCurrentSchedule.MusicLanguageDirection = music.LanguageDirection;
            updatedCurrentSchedule.MusicPublicationName = music.PublicationName;
            updatedCurrentSchedule.MusicSectionName = music.SectionName;
            updatedCurrentSchedule.MusicTrackName = music.TrackName;

            Log.Debug("ApplicationMusicReducer.OnMusicSectionSelected: Updated CurrentSchedule with MusicType={MusicType}, SectionCode={SectionCode}, SectionName={SectionName}, TrackNumber={TrackNumber}",
                music.MusicType, music.SectionCode, music.SectionName, music.TrackNumber);
        }

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }
}

