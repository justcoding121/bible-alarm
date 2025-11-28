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
            // Use MAUI's DeviceInfo for platform detection
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                // Windows: Use the alias registered in MauiProgram.cs
                // The font is registered as "FontAwesomeSolid" in ConfigureFonts
                return "FontAwesomeSolid";
            }

            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // iOS: Use the font family name from the Font Awesome 7 font file
                // The font family name inside the OTF file is "Font Awesome 7 Free Solid"
                return "Font Awesome 7 Free Solid";
            }

            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                // Android needs the font family name from the font file, not the alias
                // Format: filename#FontFamilyName (the font family name inside the OTF file)
                // This format is critical for Android reliability
                return "fa-solid-900.otf#Font Awesome 7 Free Solid";
            }
            
            // Fallback: try to get from ResourceDictionary
            return GetStringResourceForPlatform("FontAwesomeSolidId") ?? "FontAwesomeSolid";
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
}