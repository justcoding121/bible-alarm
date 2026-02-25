#nullable enable

using System;

namespace Bible.Alarm.AudioLinksHarvestor.Models;

public class MediatorTrack : IComparable
{
    public string TrackCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string LookUpPath { get; set; } = string.Empty;

    public int CompareTo(object? obj)
    {
        if (obj is not MediatorTrack other)
        {
            return 1;
        }
        if (int.TryParse(TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var thisNum) &&
            int.TryParse(other.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var otherNum))
        {
            return thisNum.CompareTo(otherNum);
        }
        return string.Compare(TrackCode, other.TrackCode, StringComparison.OrdinalIgnoreCase);
    }
}
