#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicCascadeSelectionHelperTests
{
    private sealed class FlatTracksNoSectionsMediaService : IMediaService
    {
        private readonly string publicationCode;

        public FlatTracksNoSectionsMediaService(string publicationCode) =>
            this.publicationCode = publicationCode;

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode)
        {
            if (string.Equals(versionCode, publicationCode, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(languageCode)
                && sectionCode == null)
            {
                var tracks = new SortedDictionary<string, BiblePublicationTrack>(StringComparer.Ordinal)
                {
                    ["10"] = new BiblePublicationTrack { TrackCode = "10", Title = "Ten" },
                    ["2"] = new BiblePublicationTrack { TrackCode = "2", Title = "Two" },
                };
                return Task.FromResult(tracks);
            }

            return Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());
        }

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    private sealed class FlatTracksWithLanguageMediaService(string languageCode, string publicationCode) : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string lc, string versionCode, string? sectionCode)
        {
            if (string.Equals(lc, languageCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(versionCode, publicationCode, StringComparison.OrdinalIgnoreCase)
                && sectionCode == null)
            {
                var tracks = new SortedDictionary<string, BiblePublicationTrack>(StringComparer.Ordinal)
                {
                    ["10"] = new BiblePublicationTrack { TrackCode = "10", Title = "Ten" },
                    ["2"] = new BiblePublicationTrack { TrackCode = "2", Title = "Two" },
                };
                return Task.FromResult(tracks);
            }

            return Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());
        }

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string lc, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string lc, string versionCode,
            IFetchProgress? progress = null)
        {
            if (string.Equals(lc, languageCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(versionCode, publicationCode, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());
            }

            return Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());
        }

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string lc, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string lc, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string lc, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string lc, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string lc, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string lc, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string lc, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string lc, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string lc, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    private sealed class EmptyNoLanguagePublicationMediaService : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string pub) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackAsync_without_language_uses_flat_tracks_when_sections_empty()
    {
        const string pub = "iam-flat-test";
        var media = new FlatTracksNoSectionsMediaService(pub);

        var result = await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(media, string.Empty, pub, publicationWithoutLanguage: true);

        Assert.Null(result.SectionCode);
        Assert.Equal(string.Empty, result.SectionName);
        Assert.Equal("2", result.TrackCode);
        Assert.Equal("Two", result.TrackTitle);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackAsync_without_language_returns_empty_when_no_catalog_entries()
    {
        var media = new EmptyNoLanguagePublicationMediaService();
        const string pub = "empty-no-language-catalog";

        var result = await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(media, string.Empty, pub, publicationWithoutLanguage: true);

        Assert.Null(result.SectionCode);
        Assert.Equal(string.Empty, result.SectionName);
        Assert.Equal(string.Empty, result.TrackCode);
        Assert.Equal(string.Empty, result.TrackTitle);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackAsync_with_language_uses_flat_tracks_when_sections_empty()
    {
        const string lang = "E";
        const string pub = "lang-flat-test";
        var media = new FlatTracksWithLanguageMediaService(lang, pub);

        var result = await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(media, lang, pub, publicationWithoutLanguage: false);

        Assert.Null(result.SectionCode);
        Assert.Equal(string.Empty, result.SectionName);
        Assert.Equal("2", result.TrackCode);
        Assert.Equal("Two", result.TrackTitle);
    }

    private sealed class SectionWithoutTracksMediaService(string publicationCode, bool withLanguage, string languageCode = "E")
        : IMediaService
    {
        private readonly EmptyNoLanguagePublicationMediaService inner = new();

        public void Dispose() => inner.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            inner.GetBiblePublicationLanguages(categoryName, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            inner.GetBiblePublicationTracks(languageCode, versionCode, sectionCode);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            inner.GetBiblePublications(languageCode, categoryName, downloadAll, progress, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string lc, string versionCode,
            IFetchProgress? progress = null) =>
            withLanguage
                && string.Equals(lc, languageCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(versionCode, publicationCode, StringComparison.OrdinalIgnoreCase)
                ? Task.FromResult(new SortedDictionary<string, BiblePublicationSection>(StringComparer.Ordinal)
                {
                    ["ch1"] = new BiblePublicationSection { SectionCode = "ch1", Name = "Chapter 1" },
                })
                : inner.GetBiblePublicationSections(lc, versionCode, progress);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string pub) =>
            !withLanguage && string.Equals(pub, publicationCode, StringComparison.OrdinalIgnoreCase)
                ? Task.FromResult(new SortedDictionary<string, BiblePublicationSection>(StringComparer.Ordinal)
                {
                    ["ch1"] = new BiblePublicationSection { SectionCode = "ch1", Name = "Chapter 1" },
                })
                : inner.GetSectionsForPublicationWithoutLanguage(pub);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            inner.GetBiblePublicationSection(languageCode, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            inner.GetBiblePublicationTrack(languageCode, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() => inner.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) => inner.GetMelodyMusicTracks(publicationCode);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            inner.GetMelodyMusicTracksBySection(publicationCode, sectionCode);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() => inner.GetVocalMusicLanguages();

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            inner.GetVocalMusicReleases(languageCode, downloadAll);

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            inner.GetVocalMusicTracks(languageCode, publicationCode);

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            inner.UpdateBiblePublicationTrackUrl(languageCode, versionCode, sectionCode, trackCode, url);

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            inner.UpdateVocalTrackUrl(languageCode, publicationCode, trackCode, url);

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            inner.UpdateMelodyTrackUrl(publicationCode, trackCode, url);

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) => inner.UpdateTrackUrlAsync(trackMetadata, url);

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null) =>
            inner.InvalidateBiblePublicationsCache(languageCode, categoryName);

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            inner.IsPublicationWithoutLanguageAsync(publicationCode);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            inner.GetExpectedSectionCountAsync(languageCode, publicationCode);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            inner.GetExpectedPublicationCountAsync(languageCode, categoryName, requireIsMusicForMusicCategory);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            inner.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
    }

    private sealed class SectionWithTracksMediaService(string publicationCode, bool withLanguage, string languageCode = "E")
        : IMediaService
    {
        private readonly SectionWithoutTracksMediaService sections = new(publicationCode, withLanguage, languageCode);

        public void Dispose() => sections.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            sections.GetBiblePublicationLanguages(categoryName, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string lc, string versionCode, string? sectionCode) =>
            string.Equals(versionCode, publicationCode, StringComparison.OrdinalIgnoreCase)
            && sectionCode == "ch1"
            && (!withLanguage || string.Equals(lc, languageCode, StringComparison.OrdinalIgnoreCase))
                ? Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>(StringComparer.Ordinal)
                {
                    ["5"] = new BiblePublicationTrack { TrackCode = "5", Title = "Five" },
                    ["1"] = new BiblePublicationTrack { TrackCode = "1", Title = "One" },
                })
                : sections.GetBiblePublicationTracks(lc, versionCode, sectionCode);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            sections.GetBiblePublications(languageCode, categoryName, downloadAll, progress, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string lc, string versionCode,
            IFetchProgress? progress = null) =>
            sections.GetBiblePublicationSections(lc, versionCode, progress);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string pub) =>
            sections.GetSectionsForPublicationWithoutLanguage(pub);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            sections.GetBiblePublicationSection(languageCode, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            sections.GetBiblePublicationTrack(languageCode, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() => sections.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) => sections.GetMelodyMusicTracks(publicationCode);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            sections.GetMelodyMusicTracksBySection(publicationCode, sectionCode);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() => sections.GetVocalMusicLanguages();

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            sections.GetVocalMusicReleases(languageCode, downloadAll);

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            sections.GetVocalMusicTracks(languageCode, publicationCode);

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            sections.UpdateBiblePublicationTrackUrl(languageCode, versionCode, sectionCode, trackCode, url);

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            sections.UpdateVocalTrackUrl(languageCode, publicationCode, trackCode, url);

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            sections.UpdateMelodyTrackUrl(publicationCode, trackCode, url);

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) => sections.UpdateTrackUrlAsync(trackMetadata, url);

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null) =>
            sections.InvalidateBiblePublicationsCache(languageCode, categoryName);

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            sections.IsPublicationWithoutLanguageAsync(publicationCode);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            sections.GetExpectedSectionCountAsync(languageCode, publicationCode);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            sections.GetExpectedPublicationCountAsync(languageCode, categoryName, requireIsMusicForMusicCategory);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            sections.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackAsync_without_language_returns_section_with_empty_track_when_section_has_no_tracks()
    {
        const string pub = "iam-section-empty-tracks";
        var media = new SectionWithoutTracksMediaService(pub, withLanguage: false);

        var result = await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(media, string.Empty, pub, publicationWithoutLanguage: true);

        Assert.Equal("ch1", result.SectionCode);
        Assert.Equal("Chapter 1", result.SectionName);
        Assert.Equal(string.Empty, result.TrackCode);
        Assert.Equal(string.Empty, result.TrackTitle);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackAsync_without_language_returns_first_track_in_first_section()
    {
        const string pub = "iam-section-with-tracks";
        var media = new SectionWithTracksMediaService(pub, withLanguage: false);

        var result = await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(media, string.Empty, pub, publicationWithoutLanguage: true);

        Assert.Equal("ch1", result.SectionCode);
        Assert.Equal("Chapter 1", result.SectionName);
        Assert.Equal("1", result.TrackCode);
        Assert.Equal("One", result.TrackTitle);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackAsync_with_language_returns_section_with_empty_track_when_section_has_no_tracks()
    {
        const string lang = "E";
        const string pub = "lang-section-empty-tracks";
        var media = new SectionWithoutTracksMediaService(pub, withLanguage: true, lang);

        var result = await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(media, lang, pub, publicationWithoutLanguage: false);

        Assert.Equal("ch1", result.SectionCode);
        Assert.Equal("Chapter 1", result.SectionName);
        Assert.Equal(string.Empty, result.TrackCode);
        Assert.Equal(string.Empty, result.TrackTitle);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackAsync_with_language_returns_empty_when_no_sections_or_tracks()
    {
        var media = new EmptyNoLanguagePublicationMediaService();

        var result = await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(media, "E", "empty-with-language", publicationWithoutLanguage: false);

        Assert.Null(result.SectionCode);
        Assert.Equal(string.Empty, result.SectionName);
        Assert.Equal(string.Empty, result.TrackCode);
        Assert.Equal(string.Empty, result.TrackTitle);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackAsync_with_language_returns_first_track_in_first_section()
    {
        const string lang = "E";
        const string pub = "lang-section-with-tracks";
        var media = new SectionWithTracksMediaService(pub, withLanguage: true, lang);

        var result = await MusicCascadeSelectionHelper.GetFirstSectionAndTrackAsync(media, lang, pub, publicationWithoutLanguage: false);

        Assert.Equal("ch1", result.SectionCode);
        Assert.Equal("Chapter 1", result.SectionName);
        Assert.Equal("1", result.TrackCode);
        Assert.Equal("One", result.TrackTitle);
    }
}
