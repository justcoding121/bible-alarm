#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helper class for JW.org source-related utilities.
/// Centralized location for all publication codes used for seeding.
/// Publication names are extracted directly from API responses instead of using hardcoded mappings.
/// </summary>
public static class JwSourceHelper
{
    /// <summary>
    /// Bible publication codes used for seeding.
    /// </summary>
    public static HashSet<string> BiblePublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "nwt",
        "bi12"
    };

    /// <summary>
    /// Vocal music publication codes used for seeding.
    /// </summary>
    public static HashSet<string> VocalMusicPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "osg",
        "sjjc",
        "sjji",
        "snv",
        "pksjj"
    };

    /// <summary>
    /// Melody music publication codes used for seeding.
    /// </summary>
    public static HashSet<string> MelodyMusicPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "iam"
    };

    /// <summary>
    /// Music category publications that use Mediator API for discovery (MediatorSectioned, video).
    /// Tracks come from category.media[]; same catalog path as other mediator categories (e.g. MediatorCataloger).
    /// </summary>
    public static HashSet<string> MusicMediatorPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "VODConvMusic",
        "MakingMusic",
        "VODSingToJah"
    };

    /// <summary>
    /// Publication codes that should be flagged IsMusic = true even when not under the Music category (e.g. Children Songs, Video Series songs).
    /// </summary>
    public static HashSet<string> MusicFlagPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "ChildrenSongs",
        "SeriesBJFSongs"
    };

    /// <summary>
    /// Publication codes that are in Music category (or mediator music) but should be treated as IsMusic = false for cross-pub next/prev.
    /// </summary>
    public static HashSet<string> IsMusicExcludedPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "MakingMusic"
    };

    /// <summary>
    /// Returns true if the publication code is considered music (vocal, melody, mediator music, or music-flag).
    /// Excludes codes in IsMusicExcludedPublicationCodes (e.g. MakingMusic).
    /// Used when BiblePublication is not yet cataloged so we can still skip cross-pub from music in prev/next logic.
    /// </summary>
    public static bool IsMusicPublicationCode(string? publicationCode)
    {
        if (string.IsNullOrWhiteSpace(publicationCode))
            return false;
        var code = publicationCode.Trim();
        if (IsMusicExcludedPublicationCodes.Contains(code))
            return false;
        return VocalMusicPublicationCodes.Contains(code) ||
               MelodyMusicPublicationCodes.Contains(code) ||
               MusicMediatorPublicationCodes.Contains(code) ||
               MusicFlagPublicationCodes.Contains(code);
    }

    /// <summary>
    /// Video publication codes used for seeding.
    /// </summary>
    public static HashSet<string> VideoPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "DramasGoodNews",
        "VODMoviesBibleTimes",
        "VODMoviesModernDay",
        "VODMoviesAnimated",
        "VODMoviesExtras"
    };

    /// <summary>
    /// Drama category codes used for seeding (exact casing for API/DB).
    /// </summary>
    public static HashSet<string> DramaCategoryCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        AppConstants.Media.BiblePublicationCategoryDramas,
        AppConstants.Media.BiblePublicationCodeDramaticBibleReadings,
        "DramasGoodNews",
        "VODMoviesBibleTimes",
        "VODMoviesModernDay",
        "VODMoviesAnimated",
        "VODMoviesExtras"
    };

    /// <summary>
    /// Canonical drama and Mediator API publication/category codes in exact casing for DB and Mediator API.
    /// Used to resolve normalized (lowercase) code to the form required by the API and database.
    /// </summary>
    private static readonly string[] CanonicalMediatorPublicationCodes =
    {
        AppConstants.Media.BiblePublicationCategoryDramas,
        AppConstants.Media.BiblePublicationCodeDramaticBibleReadings,
        "DramasGoodNews",
        "VODMoviesBibleTimes",
        "VODMoviesModernDay",
        "VODMoviesAnimated",
        "VODMoviesExtras",
        "SeriesDigForTreasures",
        "SeriesBJFLessons",
        "StudioMonthlyPrograms",
        "StudioTalks",
        "StudioNewsReports",
        "BJF",
        "ChildrenSongs",
        "ChildrenMovies",
        "TeenSpiritualGrowth",
        "TeenSocialLife",
        "TeenGoals",
        "TeenWhatPeersSay",
        "SeriesWhatPeersSay",
        "TeenMovies",
        "FamilyChallenges",
        "FamilyDatingMarriage",
        "FamilyWorship",
        "FamilyMovies",
        "VODPgmEvtMorningWorship",
        "VODPgmEvtSpecial",
        "VODPgmEvtGilead",
        "VODPgmEvtAnnMtg",
        "2025Convention",
        "2024Convention",
        "2023Convention",
        "2022Convention",
        "2021Convention",
        "2020Convention",
        "2019Convention",
        "2018Convention",
        "2017Convention",
        "2016Convention",
        "2015Convention",
        "2014Convention",
        "VODActivitiesTranslation",
        "VODActivitiesAVProduction",
        "VODActivitiesPrintingShipping",
        "VODActivitiesConstruction",
        "VODActivitiesReliefWork",
        "VODActivitiesTheoSchools",
        "VODActivitiesSpecialEvents",
        "VODMinistryTools",
        "VODMinistryImproveSkills",
        "VODMinistryMethods",
        "MeetingsConventions",
        "VODSampleConversations",
        "Reports",
        "VODOrgBethel",
        "AccomplishMinistry",
        "VODOrgHistory",
        "VODOrgLegal",
        "VODOrgBloodlessMedicine",
        "BibleBooks",
        "VODBibleReadingStudy",
        "VODBibleTeachings",
        "VODBibleAccounts",
        "VODBibleMedia",
        "VODBibleTranslations",
        "VODBiblePrinciples",
        "VODBibleCreation",
        "SeriesBJFSongs",
        "SeriesBibleTeachings",
        "SeriesHappyMarriage",
        "SeriesImitateFaith",
        "SeriesBibleBooks",
        "SeriesIronSharpens",
        "SeriesJehovahsFriends",
        "SeriesLearnFromThem",
        "SeriesWTLessons",
        "VODLovePeople",
        "SeriesMyTeenLife",
        "SeriesNeetaJade",
        "SeriesOrgAccomplishments",
        "SeriesOurHistory",
        "VODPureWorshipIntro",
        "SeriesBibleChangesLives",
        "SeriesGoodNews",
        "SeriesTruthTransforms",
        "SeriesOriginsLife",
        "SeriesWCGVideos",
        "SeriesWasItDesigned",
        "SeriesWhatPeersSay",
        "SeriesWhereAreTheyNow",
        "SeriesWhiteboard",
        "VODIntExpTransformations",
        "VODIntExpBlessings",
        "VODIntExpEndurance",
        "VODIntExpYouth",
        "OriginsLife",
        "VODIntExpArchives",
        "VODConvMusic",
        "MakingMusic",
        "VODSingToJah"
    };

    /// <summary>
    /// Returns the canonical (exact-case) publication code for a drama, or null if not a drama.
    /// Use for DB queries and Mediator API category key.
    /// </summary>
    public static string? GetCanonicalMediatorPublicationCode(string? normalizedPublicationCode)
    {
        if (string.IsNullOrEmpty(normalizedPublicationCode))
        {
            return null;
        }

        var normalized = normalizedPublicationCode.ToLowerInvariant();
        return CanonicalMediatorPublicationCodes.FirstOrDefault(code =>
            code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns true if the publication's primary category is Dramas (DramaCategoryCodes or VideoPublicationCodes).
    /// Use when deciding whether to show a publication under the Dramas list; excludes Series/Children pubs
    /// that are only in CanonicalMediatorPublicationCodes for API/DB resolution.
    /// </summary>
    public static bool IsInDramasCategory(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return false;
        }

        return DramaCategoryCodes.Contains(publicationCode) || VideoPublicationCodes.Contains(publicationCode);
    }

    /// <summary>
    /// Publication codes for Faith and Bible category (Mediator API).
    /// </summary>
    public static HashSet<string> FaithAndBiblePublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "BibleBooks",
        "VODBibleReadingStudy",
        "VODBibleTeachings",
        "VODBibleAccounts",
        "VODBibleMedia",
        "VODBibleTranslations",
        "VODBiblePrinciples",
        "VODBibleCreation"
    };

    /// <summary>
    /// Publication codes for Books category (flat MP3 via GETPUBMEDIALINKS).
    /// </summary>
    public static HashSet<string> BooksPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "wcg", "lff", "rr", "lvs", "lfb", "bhs", "jy", "kr", "ia", "mb", "jr", "bt", "lv", "cf", "jd", "bh", "my", "lr", "cl", "fy", "gt"
    };

    /// <summary>
    /// Publication codes for Broadcasting category (Mediator API).
    /// </summary>
    public static HashSet<string> BroadcastingPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "StudioMonthlyPrograms",
        "StudioTalks",
        "StudioNewsReports"
    };

    /// <summary>
    /// Publication codes for Yearbooks category (flat MP3 via GETPUBMEDIALINKS).
    /// </summary>
    public static HashSet<string> YearbooksPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "yb17", "yb16", "yb15", "yb14", "yb13", "yb12", "yb11", "yb10"
    };

    /// <summary>
    /// Publication codes for Brochures and Booklets category (flat MP3 via GETPUBMEDIALINKS).
    /// </summary>
    public static HashSet<string> BrochuresAndBookletsPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "lmd", "wfg", "lffi", "th", "rj", "ypq", "hf", "jl", "yc", "hl", "fg", "ll", "lc", "lf", "la", "we"
    };

    /// <summary>
    /// Publication codes for Children category.
    /// </summary>
    public static HashSet<string> ChildrenPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "SeriesBJFLessons",
        "BJF",
        "ChildrenSongs",
        "ChildrenMovies"
    };

    /// <summary>
    /// Children category publications that use Mediator API for discovery (MediatorSectioned).
    /// </summary>
    public static HashSet<string> ChildrenMediatorPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "SeriesBJFLessons",
        "BJF",
        "ChildrenSongs",
        "ChildrenMovies"
    };

    /// <summary>
    /// Publication codes for Family category (Mediator API).
    /// </summary>
    public static HashSet<string> FamilyPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "FamilyChallenges",
        "FamilyDatingMarriage",
        "FamilyWorship",
        "FamilyMovies"
    };

    /// <summary>
    /// Publication codes for Interviews and Experiences category (Mediator API).
    /// </summary>
    public static HashSet<string> InterviewsAndExperiencesPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "VODIntExpTransformations",
        "VODIntExpBlessings",
        "VODIntExpEndurance",
        "VODIntExpYouth",
        "OriginsLife",
        "VODIntExpArchives"
    };

    /// <summary>
    /// Publication codes for Meetings and Ministry category (Mediator API).
    /// </summary>
    public static HashSet<string> MeetingsAndMinistryPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "VODMinistryTools",
        "VODMinistryImproveSkills",
        "VODMinistryMethods",
        "MeetingsConventions",
        "VODSampleConversations"
    };

    /// <summary>
    /// Publication codes for Programs and Events category (Mediator API).
    /// </summary>
    public static HashSet<string> ProgramsAndEventsPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "VODPgmEvtMorningWorship",
        "VODPgmEvtSpecial",
        "VODPgmEvtGilead",
        "VODPgmEvtAnnMtg",
        "2025Convention",
        "2024Convention",
        "2023Convention",
        "2022Convention",
        "2021Convention",
        "2020Convention",
        "2019Convention",
        "2018Convention",
        "2017Convention",
        "2016Convention",
        "2015Convention",
        "2014Convention"
    };

    /// <summary>
    /// Publication codes for Series category (flat video and/or Mediator-sectioned video).
    /// </summary>
    public static HashSet<string> SeriesPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "thv",
        "SeriesDigForTreasures",
        "SeriesBJFSongs",
        "SeriesBJFLessons",
        "SeriesBibleTeachings",
        "SeriesHappyMarriage",
        "SeriesImitateFaith",
        "SeriesBibleBooks",
        "SeriesIronSharpens",
        "SeriesJehovahsFriends",
        "SeriesLearnFromThem",
        "SeriesWTLessons",
        "VODLovePeople",
        "SeriesMyTeenLife",
        "SeriesNeetaJade",
        "SeriesOrgAccomplishments",
        "SeriesOurHistory",
        "VODPureWorshipIntro",
        "SeriesBibleChangesLives",
        "SeriesGoodNews",
        "SeriesTruthTransforms",
        "SeriesOriginsLife",
        "SeriesWCGVideos",
        "SeriesWasItDesigned",
        "SeriesWhatPeersSay",
        "SeriesWhereAreTheyNow",
        "SeriesWhiteboard"
    };

    /// <summary>
    /// Series publications that use Mediator API for discovery (MediatorSectioned). Used by MediatorCataloger.
    /// </summary>
    public static HashSet<string> SeriesMediatorPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "SeriesDigForTreasures",
        "SeriesBJFSongs",
        "SeriesBJFLessons",
        "SeriesBibleTeachings",
        "SeriesHappyMarriage",
        "SeriesImitateFaith",
        "SeriesBibleBooks",
        "SeriesIronSharpens",
        "SeriesJehovahsFriends",
        "SeriesLearnFromThem",
        "SeriesWTLessons",
        "VODLovePeople",
        "SeriesMyTeenLife",
        "SeriesNeetaJade",
        "SeriesOrgAccomplishments",
        "SeriesOurHistory",
        "VODPureWorshipIntro",
        "SeriesBibleChangesLives",
        "SeriesGoodNews",
        "SeriesTruthTransforms",
        "SeriesOriginsLife",
        "SeriesWCGVideos",
        "SeriesWasItDesigned",
        "SeriesWhatPeersSay",
        "SeriesWhereAreTheyNow",
        "SeriesWhiteboard"
    };

    /// <summary>
    /// Publication codes for Activities category (Mediator API).
    /// </summary>
    public static HashSet<string> ActivitiesPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "VODActivitiesTranslation",
        "VODActivitiesAVProduction",
        "VODActivitiesPrintingShipping",
        "VODActivitiesConstruction",
        "VODActivitiesReliefWork",
        "VODActivitiesTheoSchools",
        "VODActivitiesSpecialEvents"
    };

    /// <summary>
    /// Publication codes for Organization category (Mediator API).
    /// </summary>
    public static HashSet<string> OrganizationPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "Reports",
        "VODOrgBethel",
        "AccomplishMinistry",
        "VODOrgHistory",
        "VODOrgLegal",
        "VODOrgBloodlessMedicine"
    };

    /// <summary>
    /// Publication codes for Article Series category (flat audio via GETPUBMEDIALINKS).
    /// </summary>
    public static HashSet<string> ArticleSeriesPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "mrt", // More Topics: flat MP3, pub=mrt&track=N&fileformat=MP3
        "hdu", // How Your Donations Are Used: flat MP3, pub=hdu&track=N&fileformat=MP3
        "lfs"  // Life Stories: flat MP3, pub=lfs&track=N&fileformat=MP3
    };

    /// <summary>
    /// Publication codes for The Watchtower (Magazine) category (IssueSectioned, MP3 via GETPUBMEDIALINKS with issue=).
    /// Dynamically generated: w2008..w{MagazineEndYear}.
    /// </summary>
    public static HashSet<string> WatchtowerMagazinePublicationCodes =>
        new(Enumerable.Range(MagazineHelper.MagazineStartYear, MagazineHelper.MagazineEndYear - MagazineHelper.MagazineStartYear + 1)
            .Select(y => $"w{y}"), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Awake! (Magazine) category (IssueSectioned, MP3 via GETPUBMEDIALINKS with issue=).
    /// Dynamically generated: g2008..g{MagazineEndYear}.
    /// </summary>
    public static HashSet<string> AwakeMagazinePublicationCodes =>
        new(Enumerable.Range(MagazineHelper.MagazineStartYear, MagazineHelper.MagazineEndYear - MagazineHelper.MagazineStartYear + 1)
            .Select(y => $"g{y}"), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All publication codes that use Mediator API for discovery (MediatorSectioned). Used by GetCatalogType and IsVideo.
    /// Excludes flat-audio (Article Series, Books, Yearbooks, Brochures) which use GETPUBMEDIALINKS only.
    /// </summary>
    public static HashSet<string> AllMediatorPublicationCodes
    {
        get
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in DramaCategoryCodes) set.Add(code);
            foreach (var code in VideoPublicationCodes) set.Add(code);
            foreach (var code in SeriesMediatorPublicationCodes) set.Add(code);
            foreach (var code in ChildrenMediatorPublicationCodes) set.Add(code);
            foreach (var code in BroadcastingPublicationCodes) set.Add(code);
            foreach (var code in TeenagersPublicationCodes) set.Add(code);
            foreach (var code in FamilyPublicationCodes) set.Add(code);
            foreach (var code in ProgramsAndEventsPublicationCodes) set.Add(code);
            foreach (var code in ActivitiesPublicationCodes) set.Add(code);
            foreach (var code in MeetingsAndMinistryPublicationCodes) set.Add(code);
            foreach (var code in OrganizationPublicationCodes) set.Add(code);
            foreach (var code in FaithAndBiblePublicationCodes) set.Add(code);
            foreach (var code in InterviewsAndExperiencesPublicationCodes) set.Add(code);
            foreach (var code in MusicMediatorPublicationCodes) set.Add(code);
            return set;
        }
    }

    /// <summary>
    /// Publication codes that are cataloged but not validated via Mediator API (they use GETPUBMEDIALINKS only).
    /// Empty: all video/drama use Mediator API (pub code = category key, e.g. DramasGoodNews).
    /// </summary>
    public static HashSet<string> MediatorValidationExclusionCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the Mediator API category key for the given publication code.
    /// Publication code and category key are the same for all mediator publications (e.g. DramasGoodNews).
    /// </summary>
    public static string GetMediatorCategoryKey(string publicationCode)
    {
        return publicationCode ?? string.Empty;
    }

    /// <summary>
    /// Section codes (GETPUBMEDIALINKS pub=) that use issue= rather than track= for the numeric identifier (e.g. mwbv, jwb, jwbls use YYYYMM issue ids).
    /// </summary>
    public static HashSet<string> SectionCodesUsingIssueParameter => new(StringComparer.OrdinalIgnoreCase)
    {
        "mwbv", "jwb", "jwbrd", "jwbiv", "jwbcov", "jwbam", "jwbur", "jwbls", "jwbgg"
    };

    /// <summary>
    /// Returns true if the numeric value looks like a YYYYMM issue id (e.g. 201512).
    /// When true, GETPUBMEDIALINKS should use issue= instead of track=.
    /// </summary>
    public static bool LooksLikeIssueNumber(int trackNumber)
    {
        if (trackNumber < 190101 || trackNumber > 209912)
        {
            return false;
        }
        var month = trackNumber % 100;
        return month >= 1 && month <= 12;
    }

    /// <summary>
    /// Section codes (GETPUBMEDIALINKS pub=) that have a single track; API returns files when no track param is sent.
    /// </summary>
    public static HashSet<string> SectionCodesSingleTrackNoParam => new(StringComparer.OrdinalIgnoreCase)
    {
        "ivdd", "ivno"
    };

    /// <summary>
    /// Section codes (GETPUBMEDIALINKS pub=) that have a single track; API returns files when track=0 is sent.
    /// </summary>
    public static HashSet<string> SectionCodesSingleTrackZero => new(StringComparer.OrdinalIgnoreCase)
    {
        "bhat"
    };

    /// <summary>
    /// Publication codes for Teenagers category (Mediator API).
    /// </summary>
    public static HashSet<string> TeenagersPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "TeenSpiritualGrowth",
        "TeenSocialLife",
        "TeenGoals",
        "TeenWhatPeersSay",
        "TeenMovies"
    };

    /// <summary>
    /// All language-bound publication codes that should be cataloged for English (E) in the cataloger.
    /// Includes Bible, Music (vocal only), Dramas, Video, Children, Series, Article Series, Books, Yearbooks,
    /// Brochures and Booklets, and all mediator-only categories (Broadcasting, Teenagers, Family, Programs and Events,
    /// Activities, Meetings and Ministry, Organization, Faith and Bible, Interviews and Experiences).
    /// Excludes melody (iam) which has no language.
    /// Used by EnglishSeeder and E-seed validation so every listed publication has E content with &gt;0 tracks.
    /// </summary>
    public static HashSet<string> AllPublicationCodesForEnglishSeeding
    {
        get
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in BiblePublicationCodes) set.Add(code);
            foreach (var code in VocalMusicPublicationCodes) set.Add(code);
            foreach (var code in MusicMediatorPublicationCodes) set.Add(code);
            foreach (var code in DramaCategoryCodes) set.Add(code);
            foreach (var code in VideoPublicationCodes) set.Add(code);
            foreach (var code in ChildrenPublicationCodes) set.Add(code);
            foreach (var code in SeriesPublicationCodes) set.Add(code);
            foreach (var code in BroadcastingPublicationCodes) set.Add(code);
            foreach (var code in TeenagersPublicationCodes) set.Add(code);
            foreach (var code in FamilyPublicationCodes) set.Add(code);
            foreach (var code in ProgramsAndEventsPublicationCodes) set.Add(code);
            foreach (var code in ActivitiesPublicationCodes) set.Add(code);
            foreach (var code in MeetingsAndMinistryPublicationCodes) set.Add(code);
            foreach (var code in OrganizationPublicationCodes) set.Add(code);
            foreach (var code in FaithAndBiblePublicationCodes) set.Add(code);
            foreach (var code in InterviewsAndExperiencesPublicationCodes) set.Add(code);
            foreach (var code in ArticleSeriesPublicationCodes) set.Add(code);
            foreach (var code in BooksPublicationCodes) set.Add(code);
            foreach (var code in YearbooksPublicationCodes) set.Add(code);
            foreach (var code in BrochuresAndBookletsPublicationCodes) set.Add(code);
            foreach (var code in WatchtowerMagazinePublicationCodes) set.Add(code);
            foreach (var code in AwakeMagazinePublicationCodes) set.Add(code);
            return set;
        }
    }

    /// <summary>
    /// Centralized mapping of category names to their publication codes.
    /// </summary>
    public static Dictionary<string, HashSet<string>> CategoryToPublicationCodes
    {
        get
        {
            var musicCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in VocalMusicPublicationCodes)
            {
                musicCodes.Add(code);
            }
            foreach (var code in MelodyMusicPublicationCodes)
            {
                musicCodes.Add(code);
            }
            foreach (var code in MusicMediatorPublicationCodes)
            {
                musicCodes.Add(code);
            }

            var dramaCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in DramaCategoryCodes)
            {
                dramaCodes.Add(code);
            }
            foreach (var code in VideoPublicationCodes)
            {
                dramaCodes.Add(code);
            }

            return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [AppConstants.Media.BiblePublicationCategoryBible] = BiblePublicationCodes,
                [AppConstants.Media.BiblePublicationCategoryMusic] = musicCodes,
                [AppConstants.Media.BiblePublicationCategoryDramas] = dramaCodes,
                ["FaithAndBible"] = FaithAndBiblePublicationCodes,
                ["Books"] = BooksPublicationCodes,
                ["Yearbooks"] = YearbooksPublicationCodes,
                ["Broadcasting"] = BroadcastingPublicationCodes,
                ["BrochuresAndBooklets"] = BrochuresAndBookletsPublicationCodes,
                ["Children"] = ChildrenPublicationCodes,
                ["Family"] = FamilyPublicationCodes,
                ["InterviewsAndExperiences"] = InterviewsAndExperiencesPublicationCodes,
                ["MeetingsAndMinistry"] = MeetingsAndMinistryPublicationCodes,
                ["ProgramsAndEvents"] = ProgramsAndEventsPublicationCodes,
                ["Series"] = SeriesPublicationCodes,
                ["Teenagers"] = TeenagersPublicationCodes,
                ["Activities"] = ActivitiesPublicationCodes,
                ["Organization"] = OrganizationPublicationCodes,
                ["ArticleSeries"] = ArticleSeriesPublicationCodes,
                [AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine] = WatchtowerMagazinePublicationCodes,
                [AppConstants.Media.BiblePublicationCategoryAwakeMagazine] = AwakeMagazinePublicationCodes
            };
        }
    }

    /// <summary>
    /// Gets the category name for a given publication code (for display/legacy).
    /// Returns null if the publication code is not recognized.
    /// </summary>
    public static string? GetCategoryName(string publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return null;
        }

        var normalizedCode = publicationCode.ToLowerInvariant();
        
        foreach (var (categoryName, codes) in CategoryToPublicationCodes)
        {
            if (codes.Contains(normalizedCode) || codes.Contains(publicationCode))
            {
                return categoryName;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the CategoryCode (DB) for a given publication code (e.g. Bible, Music, Dramas).
    /// Returns null if the publication code is not recognized.
    /// </summary>
    public static string? GetCategoryCode(string publicationCode)
    {
        var name = GetCategoryName(publicationCode);
        if (name == null)
        {
            return null;
        }
        return CategoryNameToCode(name);
    }

    private static string CategoryNameToCode(string categoryName)
    {
        return categoryName.Replace(" and ", "And").Replace(" ", "");
    }

    /// <summary>
    /// Gets all publication codes for a given category name.
    /// Returns an empty set if the category is not found.
    /// </summary>
    public static HashSet<string> GetPublicationCodesForCategory(string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return CategoryToPublicationCodes.TryGetValue(categoryName, out var codes)
            ? codes
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets all category codes (DB form) that this publication belongs to.
    /// Used so a single BiblePublication row can be linked to multiple categories (UX filtering only).
    /// </summary>
    public static List<string> GetCategoryCodesForPublication(string publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return new List<string>();
        }

        var result = new List<string>();
        var normalized = publicationCode.ToLowerInvariant();
        foreach (var (categoryName, codes) in CategoryToPublicationCodes)
        {
            if (codes.Contains(normalized) || codes.Contains(publicationCode))
            {
                result.Add(CategoryNameToCode(categoryName));
            }
        }

        return result;
    }

    /// <summary>
    /// Returns a display name for known publication codes when the API/DB has no name or only the code.
    /// Used for placeholders and when publication.Name is empty or equals PublicationCode.
    /// </summary>
    public static string? GetPublicationDisplayNameFallback(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return null;
        }

        return publicationCode.ToLowerInvariant() switch
        {
            "vodlffvideosad" or "bodlffvideosad" => "Enjoy Life Forever!—Videos",
            "seriesdigfortreasures" => "Dig for Treasures in God's Word",
            "seriesbjflessons" => "Bible Stories for Little Ones",
            _ => null
        };
    }

    // Publication names are extracted from API responses:
    // - Bible: parentPubName field
    // - Music: pubName field
    // - Drama: category.name field
    // - Video: pubName field
}
