using System.Net;
using Bible.Alarm.Shared.Models.Media;
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

    public int CompareTo(object obj) => string.Compare(Name, (obj as PublicationListViewItemModel)?.Name, StringComparison.Ordinal);
}
