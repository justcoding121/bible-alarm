using Bible.Alarm;

namespace FontNameResources;

public partial class FontFileResources : ResourceDictionary
{
    private static readonly FontFileResources Instance = new();

    public FontFileResources()
    {
        InitializeComponent();
    }

    public static string FontAwesomeSolid => Instance.GetStringResourceForPlatform("FontAwesomeSolidId");

    private string GetStringResourceForPlatform(string resourceKey)
    {
        if (!Instance.ContainsKey(resourceKey)) return null;
        var label = new Label();
        if (!(Instance[resourceKey] is OnPlatform<string> resource)) return string.Empty;

        var retString = resource.Platforms.Where(c => c.Platform.Contains(CurrentDevice.RuntimePlatform))
            .Select(c => c.Value).FirstOrDefault() as string;

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