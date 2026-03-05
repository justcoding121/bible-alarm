namespace Bible.Alarm.Cataloger.Models;

public class Language
{
    public string Name { get; set; }
    public string Code { get; set; }

    /// <summary>
    /// Text direction: "ltr" (left-to-right) or "rtl" (right-to-left)
    /// </summary>
    public string Direction { get; set; } = "ltr";
}

/// <summary>
/// Holds language information during cataloging
/// </summary>
public record LanguageInfo(string Name, string Direction = "ltr");
