#nullable enable
using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Explicit equality for publication-related lookup tuples so dictionary and set keys do not rely on default string equality behavior.
/// </summary>
public static class PublicationLookupKeyComparers
{
    /// <summary>
    /// (LanguageCode, PublicationCode)
    /// </summary>
    public sealed class LanguagePublication : IEqualityComparer<(string LanguageCode, string PublicationCode)>
    {
        public static readonly LanguagePublication Instance = new();

        private LanguagePublication()
        {
        }

        public bool Equals((string LanguageCode, string PublicationCode) x, (string LanguageCode, string PublicationCode) y) =>
            string.Equals(x.LanguageCode, y.LanguageCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.PublicationCode, y.PublicationCode, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string LanguageCode, string PublicationCode) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.LanguageCode),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PublicationCode));
    }

    /// <summary>
    /// (LanguageCode, PublicationCode, SectionCode)
    /// </summary>
    public sealed class LanguagePublicationSection : IEqualityComparer<(string LanguageCode, string PublicationCode, string SectionCode)>
    {
        public static readonly LanguagePublicationSection Instance = new();

        private LanguagePublicationSection()
        {
        }

        public bool Equals(
            (string LanguageCode, string PublicationCode, string SectionCode) x,
            (string LanguageCode, string PublicationCode, string SectionCode) y) =>
            string.Equals(x.LanguageCode, y.LanguageCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.PublicationCode, y.PublicationCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.SectionCode, y.SectionCode, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string LanguageCode, string PublicationCode, string SectionCode) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.LanguageCode),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PublicationCode),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SectionCode));
    }

    /// <summary>
    /// (LanguageCode, PublicationCode, SectionCode, TrackCode) with optional normalized section code.
    /// </summary>
    public sealed class LanguagePublicationNullableSectionTrack : IEqualityComparer<(string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode)>
    {
        public static readonly LanguagePublicationNullableSectionTrack Instance = new();

        private LanguagePublicationNullableSectionTrack()
        {
        }

        public bool Equals(
            (string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode) x,
            (string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode) y) =>
            string.Equals(x.LanguageCode, y.LanguageCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.PublicationCode, y.PublicationCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.SectionCode ?? string.Empty, y.SectionCode ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.TrackCode, y.TrackCode, StringComparison.Ordinal);

        public int GetHashCode((string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.LanguageCode),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PublicationCode),
                obj.SectionCode != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SectionCode) : 0,
                StringComparer.Ordinal.GetHashCode(obj.TrackCode ?? string.Empty));
    }

    /// <summary>
    /// (PublicationCode, SectionCode)
    /// </summary>
    public sealed class PublicationSection : IEqualityComparer<(string PublicationCode, string SectionCode)>
    {
        public static readonly PublicationSection Instance = new();

        private PublicationSection()
        {
        }

        public bool Equals((string PublicationCode, string SectionCode) x, (string PublicationCode, string SectionCode) y) =>
            string.Equals(x.PublicationCode, y.PublicationCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.SectionCode, y.SectionCode, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string PublicationCode, string SectionCode) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PublicationCode),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SectionCode));
    }

    /// <summary>
    /// (PublicationCode, SectionCode, TrackCode) with optional normalized section code.
    /// </summary>
    public sealed class PublicationNullableSectionTrack : IEqualityComparer<(string PublicationCode, string? SectionCode, string TrackCode)>
    {
        public static readonly PublicationNullableSectionTrack Instance = new();

        private PublicationNullableSectionTrack()
        {
        }

        public bool Equals(
            (string PublicationCode, string? SectionCode, string TrackCode) x,
            (string PublicationCode, string? SectionCode, string TrackCode) y) =>
            string.Equals(x.PublicationCode, y.PublicationCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.SectionCode ?? string.Empty, y.SectionCode ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.TrackCode, y.TrackCode, StringComparison.Ordinal);

        public int GetHashCode((string PublicationCode, string? SectionCode, string TrackCode) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PublicationCode),
                obj.SectionCode != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SectionCode) : 0,
                StringComparer.Ordinal.GetHashCode(obj.TrackCode ?? string.Empty));
    }

    /// <summary>
    /// In-memory catalog store key: publication first, optional language code (melody/vocal indexing).
    /// </summary>
    public sealed class PublicationNullableLanguageCode : IEqualityComparer<(string PublicationCode, string? LanguageCode)>
    {
        public static readonly PublicationNullableLanguageCode Instance = new();

        private PublicationNullableLanguageCode()
        {
        }

        public bool Equals(
            (string PublicationCode, string? LanguageCode) x,
            (string PublicationCode, string? LanguageCode) y) =>
            string.Equals(x.PublicationCode, y.PublicationCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.LanguageCode ?? string.Empty, y.LanguageCode ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string PublicationCode, string? LanguageCode) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PublicationCode),
                obj.LanguageCode != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.LanguageCode) : 0);
    }

    /// <summary>
    /// (PublicationCode, LanguageCode): both codes always present (e.g. schedule media groups).
    /// </summary>
    public sealed class PublicationLanguage : IEqualityComparer<(string PublicationCode, string LanguageCode)>
    {
        public static readonly PublicationLanguage Instance = new();

        private PublicationLanguage()
        {
        }

        public bool Equals((string PublicationCode, string LanguageCode) x, (string PublicationCode, string LanguageCode) y) =>
            string.Equals(x.PublicationCode, y.PublicationCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.LanguageCode, y.LanguageCode, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string PublicationCode, string LanguageCode) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PublicationCode),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.LanguageCode));
    }
}
