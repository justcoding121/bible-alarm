using Foundation;
using Microsoft.Maui.Platform;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Handlers;

public class SearchBarHandler : Microsoft.Maui.Handlers.SearchBarHandler
{
    protected override MauiSearchBar CreatePlatformView()
    {
        var searchBar = base.CreatePlatformView();

        if (searchBar is UISearchBar uiSearchBar)
        {
            uiSearchBar.BackgroundImage = new UIImage();
            uiSearchBar.BarTintColor = UIColor.Clear;
            uiSearchBar.SearchBarStyle = UISearchBarStyle.Minimal;
            uiSearchBar.Layer.BorderWidth = 0;
            uiSearchBar.Layer.BorderColor = null;
            RemoveSearchBarBackground(uiSearchBar);
        }

        return searchBar;
    }

    protected override void ConnectHandler(MauiSearchBar platformView)
    {
        base.ConnectHandler(platformView);
        if (platformView is UISearchBar uiSearchBar)
            RemoveSearchBarBackground(uiSearchBar);
    }

    private static void RemoveSearchBarBackground(UISearchBar searchBar)
    {
        foreach (var subview in searchBar.Subviews)
        {
            if (subview.Class.Name == "UISearchBarBackground")
            {
                subview.RemoveFromSuperview();
                break;
            }
        }
    }
}

