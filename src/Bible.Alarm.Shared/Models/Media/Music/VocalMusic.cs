#nullable enable

using System.Collections.Generic;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Models.Media.Music;

/// <summary>
/// Wrapper for BiblePublication used for Vocal music (Music category with LanguageId).
/// Vocal music publications are BiblePublications under Music category with a language foreign key.
/// </summary>
public class VocalMusic
{
    public BiblePublication Publication { get; set; } = null!;

    public int Id => Publication.Id;
    public string Code => Publication.PublicationCode;
    public string Name => Publication.Name;
    public int CategoryId => Publication.PrimaryCategoryId;
    public Category Category => Publication.PrimaryCategory!;
    public int? LanguageId => Publication.LanguageId;
    public Language? Language => Publication.Language;
    public List<BiblePublicationSection> Sections => Publication.Sections;
    public List<BiblePublicationTrack> Tracks => Publication.Tracks;
    public bool IsVideo => Publication.IsVideo;

    public static implicit operator BiblePublication(VocalMusic vocalMusic) => vocalMusic.Publication;
    public static implicit operator VocalMusic(BiblePublication publication) => new VocalMusic { Publication = publication };
}
