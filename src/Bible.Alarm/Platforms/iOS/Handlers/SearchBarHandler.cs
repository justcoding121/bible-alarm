using Microsoft.Maui.Platform;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Handlers;

public class SearchBarHandler : Microsoft.Maui.Handlers.SearchBarHandler
{
    protected override MauiSearchBar CreatePlatformView()
    {
        var searchBar = base.CreatePlatformView();
        
        // Remove black squares/borders on iOS SearchBar
        if (searchBar is UISearchBar uiSearchBar)
        {
            uiSearchBar.BackgroundImage = new UIImage();
            uiSearchBar.BarTintColor = UIColor.Clear;
            uiSearchBar.SearchBarStyle = UISearchBarStyle.Minimal;
        }
        
        return searchBar;
    }
}

