#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class TranslatedPublicationTests
{
    [Fact]
    public void Equals_requires_same_name_and_language_id_regardless_of_publication_code()
    {
        var language = new Language
        {
            LanguageCode = "E",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var left = new BiblePublication
        {
            Name = "Same title",
            PublicationCode = "code-a",
            LanguageId = 5,
            Language = language,
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = false,
        };

        var right = new BiblePublication
        {
            Name = "Same title",
            PublicationCode = "code-b",
            LanguageId = 5,
            Language = language,
            Sections = [],
            Tracks = [],
            IsVideo = false,
            IsMusic = false,
        };

        Assert.True(((TranslatedPublication)left).Equals((TranslatedPublication)right));
        Assert.NotEqual(left.PublicationCode, right.PublicationCode);
    }
}
