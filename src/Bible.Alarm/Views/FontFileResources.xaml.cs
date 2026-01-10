#nullable enable
using Bible.Alarm.Common;
using Microsoft.Maui.Controls.Xaml;

namespace Bible.Alarm.Views;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class FontFileResources : ResourceDictionary
{
    private static FontFileResources? instance;
    private static readonly Lock @lock = new();

    public FontFileResources()
    {
        InitializeComponent();
    }

    private static FontFileResources Instance
    {
        get
        {
            if (instance == null)
            {
                lock (@lock)
                {
                    if (instance == null)
                    {
                        instance = new FontFileResources();
                    }
                }
            }
            return instance;
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
        if (!Instance.ContainsKey(resourceKey))
        {
            return null;
        }

        var label = new Label();
        if (Instance[resourceKey] is not OnPlatform<string> resource)
        {
            return string.Empty;
        }

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

public sealed class GlyphNames
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
    // Font Awesome music note icon
    public static string Music = "\uf001";
    // Font Awesome rotate-right icon (retry)
    public static string Retry = "\uf01e";
    // Font Awesome exclamation-triangle icon (for alarm settings)
    public static string ExclamationTriangle = "\uf071";
    // Font Awesome checkmark icon
    public static string Check = "\uf00c";
    // Font Awesome book-bible icon (for Bible type selection)
    public static string Bible = "\uf647";
}
