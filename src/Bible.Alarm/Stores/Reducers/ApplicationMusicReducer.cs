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
        // Cascade rule: only the changed row and rows below are updated; do not clear the language row
        // when only publication/track changed (e.g. switching from iam to osg keeps "English").
        var updatedCurrentSchedule = state.CurrentSchedule;
        if (updatedCurrentSchedule != null && action.CurrentMusic != null)
        {
            var music = action.CurrentMusic;
            updatedCurrentSchedule = updatedCurrentSchedule.DeepClone();
            // For melody (no-language pub), preserve existing music display language (e.g. MY) so it can be saved and restored on view schedule.
            updatedCurrentSchedule.MusicLanguageCode = !string.IsNullOrWhiteSpace(music.LanguageCode)
                ? music.LanguageCode
                : (updatedCurrentSchedule.MusicLanguageCode ?? Bible.Alarm.Shared.Constants.AppConstants.Media.DefaultLanguageCode);
            updatedCurrentSchedule.MusicPublicationCode = music.PublicationCode;
            updatedCurrentSchedule.MusicSectionCode = music.SectionCode; // Clear if null (when language/publication changes)
            updatedCurrentSchedule.MusicTrackCode = music.TrackCode;
            updatedCurrentSchedule.MusicRepeat = music.Repeat;
            // Display names: use action when provided. For melody, preserve existing music language name/direction only.
            updatedCurrentSchedule.MusicLanguageName = !string.IsNullOrEmpty(music.LanguageName)
                ? music.LanguageName
                : (string.IsNullOrEmpty(music.LanguageCode) ? updatedCurrentSchedule.MusicLanguageName : updatedCurrentSchedule.MusicLanguageName);
            updatedCurrentSchedule.MusicLanguageDirection = !string.IsNullOrEmpty(music.LanguageDirection)
                ? music.LanguageDirection
                : (string.IsNullOrEmpty(music.LanguageCode) ? updatedCurrentSchedule.MusicLanguageDirection : updatedCurrentSchedule.MusicLanguageDirection);
            updatedCurrentSchedule.MusicPublicationName = music.PublicationName;
            updatedCurrentSchedule.MusicSectionName = music.SectionName; // Clear if null (when language/publication changes)
            updatedCurrentSchedule.MusicTrackName = music.TrackName;

            Log.Debug("ApplicationMusicReducer.OnMusicTrackSelected: Updated CurrentSchedule with LanguageCode={LanguageCode}, TrackCode={TrackCode}, TrackName={TrackName}",
                updatedCurrentSchedule.MusicLanguageCode ?? "(null)", music.TrackCode, music.TrackName);
        }

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }

    [ReducerMethod]
    public static ApplicationState OnMusicSectionSelected(ApplicationState state, MusicSectionSelectedAction action)
    {
        // IMPORTANT: Update CurrentSchedule synchronously here to ensure schedule page shows
        // the new section immediately when modal closes. The async effect runs too late.
        // Preserve language row when action has no LanguageName but has LanguageCode (same as OnMusicTrackSelected).
        var updatedCurrentSchedule = state.CurrentSchedule;
        if (updatedCurrentSchedule != null && action.CurrentMusic != null)
        {
            var music = action.CurrentMusic;
            updatedCurrentSchedule = updatedCurrentSchedule.DeepClone();
            // For melody (no-language pub), preserve existing music display language (e.g. MY) so it can be saved and restored on view schedule.
            updatedCurrentSchedule.MusicLanguageCode = !string.IsNullOrWhiteSpace(music.LanguageCode)
                ? music.LanguageCode
                : (updatedCurrentSchedule.MusicLanguageCode ?? Bible.Alarm.Shared.Constants.AppConstants.Media.DefaultLanguageCode);
            updatedCurrentSchedule.MusicPublicationCode = music.PublicationCode;
            updatedCurrentSchedule.MusicSectionCode = music.SectionCode;
            updatedCurrentSchedule.MusicTrackCode = music.TrackCode;
            updatedCurrentSchedule.MusicRepeat = music.Repeat;
            updatedCurrentSchedule.MusicLanguageName = !string.IsNullOrEmpty(music.LanguageName)
                ? music.LanguageName
                : (string.IsNullOrEmpty(music.LanguageCode) ? updatedCurrentSchedule.MusicLanguageName : updatedCurrentSchedule.MusicLanguageName);
            updatedCurrentSchedule.MusicLanguageDirection = !string.IsNullOrEmpty(music.LanguageDirection)
                ? music.LanguageDirection
                : (string.IsNullOrEmpty(music.LanguageCode) ? updatedCurrentSchedule.MusicLanguageDirection : updatedCurrentSchedule.MusicLanguageDirection);
            updatedCurrentSchedule.MusicPublicationName = music.PublicationName;
            updatedCurrentSchedule.MusicSectionName = music.SectionName;
            updatedCurrentSchedule.MusicTrackName = music.TrackName;

            Log.Debug("ApplicationMusicReducer.OnMusicSectionSelected: Updated CurrentSchedule with LanguageCode={LanguageCode}, SectionCode={SectionCode}, SectionName={SectionName}, TrackCode={TrackCode}",
                updatedCurrentSchedule.MusicLanguageCode ?? "(null)", music.SectionCode, music.SectionName, music.TrackCode);
        }

        return StateFactory.CreateUpdatedState(state, updatedCurrentSchedule);
    }
}

