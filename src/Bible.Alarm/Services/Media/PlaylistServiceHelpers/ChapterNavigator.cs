#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.Bible;
using Serilog;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles Bible chapter and section navigation.
/// </summary>
public sealed class ChapterNavigator(IMediaService mediaService)
{
    /// <summary>
    /// Gets the next Bible chapter.
    /// </summary>
    public async Task<KeyValuePair<BibleSection, BiblePublicationChapter>> GetNextBiblePublicationChapter(
        string languageCode,
        string publicationCode,
        int sectionNumber,
        int chapter)
    {
        var currentSection = await mediaService.GetBibleSection(languageCode, publicationCode, sectionNumber) 
            ?? throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={sectionNumber}");
        
        var chapters = await mediaService.GetBiblePublicationChapters(languageCode, publicationCode, sectionNumber);
        var nextChapter = chapters.SkipWhile(kvp => kvp.Key <= chapter).FirstOrDefault();

        if (!nextChapter.Equals(default(KeyValuePair<int, BiblePublicationChapter>)))
        {
            return new KeyValuePair<BibleSection, BiblePublicationChapter>(currentSection, nextChapter.Value);
        }

        var nextSection = await GetNextBibleSection(languageCode, publicationCode, sectionNumber);
        if (nextSection.Value == null)
        {
            throw new InvalidOperationException($"Next bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={sectionNumber}");
        }

        chapters = await mediaService.GetBiblePublicationChapters(languageCode, publicationCode, nextSection.Key);
        if (chapters.Count == 0)
        {
            throw new InvalidOperationException($"No chapters in next section: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={nextSection.Key}");
        }

        // Start at the first chapter of the next section (index 0)
        return new KeyValuePair<BibleSection, BiblePublicationChapter>(nextSection.Value, chapters.ElementAt(0).Value);
    }

    /// <summary>
    /// Gets the previous Bible chapter.
    /// </summary>
    public async Task<KeyValuePair<BibleSection, BiblePublicationChapter>> GetPreviousBiblePublicationChapter(
        string languageCode,
        string publicationCode,
        int sectionNumber,
        int chapter)
    {
        var currentSection = await mediaService.GetBibleSection(languageCode, publicationCode, sectionNumber) 
            ?? throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={sectionNumber}");
        
        var chapters = await mediaService.GetBiblePublicationChapters(languageCode, publicationCode, sectionNumber);
        var previousChapter = chapters.Reverse().SkipWhile(kvp => kvp.Key >= chapter).FirstOrDefault();

        if (!previousChapter.Equals(default(KeyValuePair<int, BiblePublicationChapter>)))
        {
            return new KeyValuePair<BibleSection, BiblePublicationChapter>(currentSection, previousChapter.Value);
        }

        var previousSection = await GetPreviousBibleSection(languageCode, publicationCode, sectionNumber);
        if (previousSection.Value == null)
        {
            throw new InvalidOperationException($"Previous bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={sectionNumber}");
        }

        chapters = await mediaService.GetBiblePublicationChapters(languageCode, publicationCode, previousSection.Key);
        if (chapters.Count == 0)
        {
            throw new InvalidOperationException($"No chapters in previous section: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={previousSection.Key}");
        }

        return new KeyValuePair<BibleSection, BiblePublicationChapter>(previousSection.Value, chapters.ElementAt(chapters.Count - 1).Value);
    }

    /// <summary>
    /// Gets the previous Bible section.
    /// </summary>
    public async Task<KeyValuePair<int, BibleSection>> GetPreviousBibleSection(
        string languageCode,
        string publicationCode,
        int sectionNumber)
    {
        var sections = await mediaService.GetBibleSections(languageCode, publicationCode);
        if (sections.Count == 0)
        {
            throw new InvalidOperationException($"No bible sections found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var previousSection = sections.Reverse().SkipWhile(kvp => kvp.Key >= sectionNumber).FirstOrDefault();

        if (!previousSection.Equals(default(KeyValuePair<int, BibleSection>)))
        {
            return previousSection;
        }

        var maxKey = sections.Keys.Max();
        if (!sections.TryGetValue(maxKey, out var maxSection))
        {
            throw new InvalidOperationException($"Bible section with key {maxKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<int, BibleSection>(maxKey, maxSection);
    }

    /// <summary>
    /// Gets the next Bible section.
    /// </summary>
    public async Task<KeyValuePair<int, BibleSection>> GetNextBibleSection(
        string languageCode,
        string publicationCode,
        int sectionNumber)
    {
        var sections = await mediaService.GetBibleSections(languageCode, publicationCode);
        if (sections.Count == 0)
        {
            throw new InvalidOperationException($"No bible sections found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var nextSection = sections.SkipWhile(kvp => kvp.Key <= sectionNumber).FirstOrDefault();

        if (!nextSection.Equals(default(KeyValuePair<int, BibleSection>)))
        {
            return nextSection;
        }

        var minKey = sections.Keys.Min();
        if (!sections.TryGetValue(minKey, out var minSection))
        {
            throw new InvalidOperationException($"Bible section with key {minKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<int, BibleSection>(minKey, minSection);
    }
}
