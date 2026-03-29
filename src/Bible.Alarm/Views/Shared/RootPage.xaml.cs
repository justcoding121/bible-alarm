#nullable enable

namespace Bible.Alarm.Views.Shared;

public partial class RootPage : ContentPage
{
    public NavigationPage InnerNavigationPage { get; }

    public RootPage(NavigationPage navigationPage)
    {
        InitializeComponent();
        InnerNavigationPage = navigationPage;

        Grid.SetRow(navigationPage, 0);
        RootGrid.Add(navigationPage);
    }

    public void SetMiniBarContent(View miniBar)
    {
        MiniBarHost.Content = miniBar;
    }

    public void SetMiniBarVisible(bool visible)
    {
        MiniBarHost.IsVisible = visible;
    }

    public bool IsMiniBarVisible => MiniBarHost.IsVisible;
}
