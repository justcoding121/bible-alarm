using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Maui.MediaSource;
using MediaSourceType = CommunityToolkit.Maui.MediaSource.MediaSource;

namespace CommunityToolkit.Maui.Converters;

/// <summary>
/// A <see cref="TypeConverter"/> specific to converting a string value to a <see cref="MediaSource"/>.
/// </summary>
public sealed class MediaSourceConverter : TypeConverter
{
    const string embeddedResourcePrefix = "embed://";
    const string fileSystemPrefix = "filesystem://";

    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string);
    }

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string);
    }

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
    {
        var valueAsString = value?.ToString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(valueAsString))
        {
            return null;
        }

        var valueAsStringLowercase = valueAsString.ToLowerInvariant();

        if (valueAsStringLowercase.StartsWith(embeddedResourcePrefix))
        {
            return MediaSourceType.FromResource(
                valueAsString[embeddedResourcePrefix.Length..]);
        }

        if (valueAsStringLowercase.StartsWith(fileSystemPrefix))
        {
            return MediaSourceType.FromFile(valueAsString[fileSystemPrefix.Length..]);
        }

        return Uri.TryCreate(valueAsString, UriKind.Absolute, out var uri) && uri.Scheme != "file"
            ? MediaSourceType.FromUri(uri)
            : MediaSourceType.FromFile(valueAsString);
    }

    /// <inheritdoc/>
    public override object ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        return value switch
        {
            UriMediaSource uriMediaSource => uriMediaSource.ToString(),
            FileMediaSource fileMediaSource => fileMediaSource.ToString(),
            ResourceMediaSource resourceMediaSource => resourceMediaSource.ToString(),
            MediaSourceType => string.Empty,
            _ => throw new ArgumentException("Invalid Media Source", nameof(value))
        };
    }
}