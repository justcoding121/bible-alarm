#nullable enable

using Bible.Alarm.Platforms.Windows.Services.UI;
using Windows.Data.Xml.Dom;

namespace Bible.Alarm.Tests;

public sealed class WindowsToastXmlFactoryTests
{
    [Fact]
    public void CreateToastXml_sets_alarm_scenario_and_launch_schedule_id()
    {
        XmlDocument xml = WindowsToastXmlFactory.CreateToastXml("ignored-title", "Wake up", scheduleId: 42);
        var s = xml.GetXml();

        Assert.Contains("scenario=\"alarm\"", s);
        Assert.Contains("Wake up", s);
        Assert.Contains("launch=\"42\"", s);
    }

    [Fact]
    public void CreateToastXml_omits_body_element_when_body_blank()
    {
        XmlDocument xml = WindowsToastXmlFactory.CreateToastXml("x", "   ", scheduleId: 1);
        var s = xml.GetXml();

        Assert.DoesNotContain("Wake up", s);
        Assert.Contains("<toast", s);
    }

    [Fact]
    public void CreateMediaToastXml_embeds_https_artwork_and_title()
    {
        XmlDocument xml = WindowsToastXmlFactory.CreateMediaToastXml(
            title: "Song",
            subtitle: "Artist line",
            body: null,
            artworkUrl: "https://cdn.example.com/a.png",
            canPlayNext: false,
            canPlayPrevious: false,
            isPlaying: true);

        var s = xml.GetXml();

        Assert.Contains("https://cdn.example.com/a.png", s);
        Assert.Contains("Song", s);
        Assert.Contains("Artist line", s);
        Assert.Contains("scenario=\"reminder\"", s);
        Assert.Contains("silent=\"true\"", s);
    }

    [Fact]
    public void CreateMediaToastXml_skips_image_when_artwork_missing()
    {
        XmlDocument xml = WindowsToastXmlFactory.CreateMediaToastXml(
            title: "Only title",
            subtitle: "",
            body: null,
            artworkUrl: null,
            canPlayNext: true,
            canPlayPrevious: true,
            isPlaying: false);

        var s = xml.GetXml();

        Assert.Contains("Only title", s);
        Assert.DoesNotContain("<image", s);
    }
}
