#nullable enable
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class PublicationListViewItemModel(Publication publication) : ObservableObject, IComparable
{
    private bool isSelected;
    private bool isNavigating;
    private double downloadProgress = -1.0; // -1 means "not set", >= 0 means "fetch in progress or completed"

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsNavigating
    {
        get => isNavigating;
        set => SetProperty(ref isNavigating, value);
    }

    /// <summary>
    /// Download progress for this publication selection operation (0.0 to 1.0).
    /// Used to show per-row progress percent alongside the spinner.
    /// </summary>
    public double DownloadProgress
    {
        get => downloadProgress;
        set
        {
            var clamped = value;
            // Allow -1.0 as sentinel value meaning "not set", otherwise clamp to 0.0-1.0
            if (clamped >= 0.0 && clamped > 1.0) clamped = 1.0;
            if (clamped < -1.0) clamped = -1.0;

            if (SetProperty(ref downloadProgress, clamped))
            {
                OnPropertyChanged(nameof(DownloadProgressText));
            }
        }
    }

    public string DownloadProgressText
    {
        get
        {
            // Return empty string if progress is not set (< 0), otherwise return percentage
            if (downloadProgress < 0.0)
            {
                return string.Empty;
            }
            var percent = (int)Math.Round(downloadProgress * 100.0, MidpointRounding.AwayFromZero);
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            return $"{percent}%";
        }
    }

    /// <summary>
    /// Gets the publication name with HTML entities decoded (e.g., &#160; → space) and non-breaking spaces replaced with regular spaces.
    /// Uses a fallback display name when the stored name is empty or equals the publication code (e.g. VODLFFVideosAD).
    /// </summary>
    public string Name
    {
        get
        {
            var rawName = publication.Name;
            var effectiveName = string.IsNullOrWhiteSpace(rawName) ||
                               string.Equals(rawName.Trim(), publication.PublicationCode, StringComparison.OrdinalIgnoreCase)
                ? (JwSourceHelper.GetPublicationDisplayNameFallback(publication.PublicationCode) ?? rawName ?? publication.PublicationCode)
                : rawName;
            return MediaTrackTitleHelper.DecodeHtmlTitle(effectiveName);
        }
    }
    public string Code => publication.PublicationCode;

    /// <summary>
    /// True when this publication is stored without a language FK (LanguageId == null),
    /// meaning callers should query sections/tracks WITHOUT a language code.
    /// </summary>
    public bool IsPublicationWithoutLanguage =>
        publication is BiblePublication biblePublication && biblePublication.LanguageId == null;

    /// <summary>
    /// True when this publication is stored WITH a language FK (LanguageId != null).
    /// </summary>
    public bool HasLanguageId =>
        publication is BiblePublication biblePublication && biblePublication.LanguageId != null;

    /// <summary>
    /// Language code for this publication row (when available).
    /// For many selection flows, this avoids re-querying the DB just to rediscover language.
    /// </summary>
    public string? PublicationLanguageCode =>
        publication is BiblePublication biblePublication ? biblePublication.Language?.LanguageCode : null;

    public int CompareTo(object? obj)
    {
        if (obj is not PublicationListViewItemModel other)
        {
            return 1;
        }

        // Prefer sorting by publication code priority (e.g. nwt first), then by name.
        var codeCompare = PublicationCodeHelper.PublicationCodeComparer.Compare(Code, other.Code);
        if (codeCompare != 0)
        {
            return codeCompare;
        }

        return string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }
}
