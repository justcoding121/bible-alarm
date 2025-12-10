#nullable enable
using Bible.Alarm.Common;

namespace Bible.Alarm.Views;

public partial class FontFileResources : ResourceDictionary
{
    private static FontFileResources? _instance;
    private static readonly object _lock = new object();

    public FontFileResources()
    {
        InitializeComponent();
    }

    private static FontFileResources Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new FontFileResources();
                    }
                }
            }
            return _instance;
        }
    }

    public static string FontAwesomeSolid
    {
        get
        {
            // Use the alias registered in MauiProgram.cs for all platforms
            // This is the simplest and most reliable approach
            // The font is registered as "FontAwesomeSolid" in ConfigureFonts
            return "FontAwesomeSolid";
        }
    }

    private static string? GetStringResourceForPlatform(string resourceKey)
    {
        if (!Instance.ContainsKey(resourceKey)) return null;
        var label = new Label();
        if (!(Instance[resourceKey] is OnPlatform<string> resource)) return string.Empty;

        // Try to match using DeviceInfo first
        string platformName;
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            platformName = "WinUI";
        }
        else if (DeviceInfo.Platform == DevicePlatform.iOS)
        {
            platformName = "iOS";
        }
        else if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            platformName = "Android";
        }
        else
        {
            platformName = CurrentDevice.RuntimePlatform ?? "";
        }

        var retString = resource.Platforms.Where(c => c.Platform.Contains(platformName))
            .Select<On, object>(c => c.Value).FirstOrDefault() as string;

        return retString ?? "NOFONT";
    }
}

public class GlyphNames
{
    public static string Plus = "\uf067";
    public static string Search = "\uf002";
    public static string Play = "\uf04b";
    public static string Pause = "\uf04c";
    public static string Stop = "\uf04d";
    public static string Left = "\uf053";
    public static string Right = "\uf054";
    public static string Language = "\uf1ab";
    public static string Bell = "\uf0f3";
    public static string Repeat = "\uf0e2";

    public static string Next = "\uf050";
    public static string Previous = "\uf049";

    public static string Forward = "\uf04e";
    public static string Backward = "\uf04a";
    public static string Trash = "\uf2ed";
    // Font Awesome circle-info icon
    public static string InfoCircle = "\uf05a";
}