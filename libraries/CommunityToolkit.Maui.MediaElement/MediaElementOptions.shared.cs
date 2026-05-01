using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Maui.Primitives;

namespace CommunityToolkit.Maui;

/// <summary>
/// Construction options for MediaElement, for example, to create an Android SurfaceView or TextureView
/// </summary>
[SuppressMessage("SonarAnalyzer.CSharp", "S1118",
    Justification = "Internal instance constructors integrate with MauiAppBuilder.")]
public class MediaElementOptions
{
    internal MediaElementOptions()
    {

    }

    internal MediaElementOptions(in MauiAppBuilder _) : this()
    {
    }

    /// <summary>
    /// Set Android View type for MediaElement as SurfaceView or TextureView on construction
    /// </summary>
    internal static AndroidViewType DefaultAndroidViewType { get; private set; } = AndroidViewType.SurfaceView;

    /// <summary>
    /// Set Android View type for MediaElement as SurfaceView or TextureView on construction
    /// </summary>
    public static void SetDefaultAndroidViewType(AndroidViewType androidViewType)
    {
        DefaultAndroidViewType = androidViewType;
    }
}