#nullable enable

using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Views;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class FontFileResources : ResourceDictionary
{
    public FontFileResources()
    {
        InitializeComponent();
    }

    public static string FontAwesomeSolid
    {
        get
        {
            // Use the alias registered in MauiProgram.cs for all platforms (see AppConstants.Fonts).
            return AppConstants.Fonts.FontAwesomeSolidAlias;
        }
    }
}

public sealed class GlyphNames
{
    public const string Plus = "\uf067";
    public const string Search = "\uf002";
    public const string Play = "\uf04b";
    public const string Pause = "\uf04c";
    public const string Stop = "\uf04d";
    public const string Left = "\uf053";
    public const string Right = "\uf054";
    // Font Awesome arrow-left icon (for back/close buttons)
    public const string ArrowLeft = "\uf060";
    public const string Language = "\uf1ab";
    public const string Bell = "\uf0f3";
    public const string Repeat = "\uf0e2";

    public const string Next = "\uf050";
    public const string Previous = "\uf049";

    public const string Forward = "\uf04e";
    public const string Backward = "\uf04a";
    public const string Trash = "\uf2ed";
    // Font Awesome circle-info icon
    public const string InfoCircle = "\uf05a";
    // Font Awesome music note icon
    public const string Music = "\uf001";
    // Font Awesome rotate-right icon (retry)
    public const string Retry = "\uf01e";
    // Font Awesome exclamation-triangle icon (for alarm settings)
    public const string ExclamationTriangle = "\uf071";
    // Font Awesome triangle icon (simple triangle pointing up)
    public const string Triangle = "\uf0d8";
    // Font Awesome checkmark icon
    public const string Check = "\uf00c";
    // Font Awesome section-bible icon (for Bible type selection)
    public const string Bible = "\uf647";
    // Font Awesome folder icon (for category selection)
    public const string Folder = "\uf07b";
    // Font Awesome xmark icon (for cancel/close buttons)
    public const string Xmark = "\uf00d";
    // Font Awesome floppy-disk icon (for save buttons)
    public const string FloppyDisk = "\uf0c7";
    // Font Awesome chevron-down icon (minimize playback)
    public const string ChevronDown = "\uf078";
    // Font Awesome chevron-up icon (maximize playback)
    public const string ChevronUp = "\uf077";
}
