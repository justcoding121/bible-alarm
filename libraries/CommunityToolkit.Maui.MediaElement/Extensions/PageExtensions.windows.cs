#if WINDOWS
namespace CommunityToolkit.Maui.Extensions;

static partial class PageExtensions
{
    internal static class ParentWindow
    {
        static Page CurrentPage => GetCurrentPage(Application.Current?.Windows[^1].Page ?? throw new InvalidOperationException($"{nameof(Page)} cannot be null."));
        /// <summary>
        /// Checks if the parent window is null.
        /// </summary>
        public static bool Exists
        {
            get
            {
                if (CurrentPage.GetParentWindow() is null)
                {
                    return false;
                }
                if (CurrentPage.GetParentWindow().Handler is null)
                {
                    return false;
                }

                return CurrentPage.GetParentWindow().Handler?.PlatformView is not null;
            }
        }
    }
}
#endif
