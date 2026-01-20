#nullable enable

using System.Collections.Generic;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Models.Media.Music;

/// <summary>
/// Wrapper for BiblePublication used for Kingdom Melodies (Music category without LanguageId).
/// Kingdom Melodies are BiblePublications under Music category without a language foreign key.
/// </summary>
public class MelodyMusic
{
    public BiblePublication Publication { get; set; } = null!;

    // Expose BiblePublication properties for convenience
    public int Id => Publication.Id;
    public string Code => Publication.PublicationCode;
    public string Name => Publication.Name;
    public int CategoryId => Publication.CategoryId;
    public Category Category => Publication.Category;
    public int? LanguageId => Publication.LanguageId;
    public Language? Language => Publication.Language;
    public List<UrlParam> UrlParams => Publication.UrlParams;
    public List<BiblePublicationSection> Sections => Publication.Sections;
    public List<BiblePublicationTrack> Tracks => Publication.Tracks;
    public bool IsVideo => Publication.IsVideo;

    public static implicit operator BiblePublication(MelodyMusic melodyMusic) => melodyMusic.Publication;
    public static implicit operator MelodyMusic(BiblePublication publication) => new MelodyMusic { Publication = publication };
}
