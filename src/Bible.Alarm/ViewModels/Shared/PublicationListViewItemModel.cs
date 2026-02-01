#nullable enable
using System.Net;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class PublicationListViewItemModel(Publication publication) : ObservableObject, IComparable
{
    private bool isSelected;
    private bool isNavigating;

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
    /// Gets the publication name with HTML entities decoded (e.g., &#160; → space) and non-breaking spaces replaced with regular spaces.
    /// </summary>
    public string Name => WebUtility.HtmlDecode(publication.Name).Replace('\u00A0', ' ');
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

    public int CompareTo(object obj) => string.Compare(Name, (obj as PublicationListViewItemModel)?.Name, StringComparison.Ordinal);
}
