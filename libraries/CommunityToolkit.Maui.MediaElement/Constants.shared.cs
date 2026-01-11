[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/2022/maui/toolkit", "CommunityToolkit.Maui.Converters")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/2022/maui/toolkit", "CommunityToolkit.Maui.Core")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/2022/maui/toolkit", "CommunityToolkit.Maui.Core.Handlers")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/2022/maui/toolkit", "CommunityToolkit.Maui.Core.Views")]
[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/2022/maui/toolkit", "CommunityToolkit.Maui.Views")]

[assembly: Microsoft.Maui.Controls.XmlnsPrefix("http://schemas.microsoft.com/dotnet/2022/maui/toolkit", "toolkit")]

namespace CommunityToolkit.Maui;

static class Constants
{
    public const string XamlNamespace = "http://schemas.microsoft.com/dotnet/2022/maui/toolkit";

    public const string CommunityToolkitNamespacePrefix = "CommunityToolkit.Maui.";
}