using Android.OS;
using Android.Runtime;
using AndroidX.Media3.Common;
using AndroidX.Media3.Session;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Google.Common.Util.Concurrent;
using Java.Util;

namespace Bible.Alarm.Platforms.Android.Services.Media3;

/// <summary>
/// Library and session callback for the inactive Media3 service.
/// Every controller may connect. Browse results are limited to car hosts.
/// </summary>
[Preserve(AllMembers = true)]
public sealed class LibraryCallback : Java.Lang.Object, MediaLibraryService.MediaLibrarySession.ICallback
{
    public IListenableFuture? OnConnectAsync(MediaSession? session, MediaSession.ControllerInfo? controller)
    {
        _ = session;
        var sessionCommands = new SessionCommands.Builder()
            .Add(SessionCommand.CommandCodeLibraryGetLibraryRoot)!
            .Add(SessionCommand.CommandCodeLibraryGetChildren)!
            .Add(SessionCommand.CommandCodeLibraryGetItem)!
            .Add(SessionCommand.CommandCodeLibrarySearch)!
            .Add(SessionCommand.CommandCodeLibraryGetSearchResult)!
            .Add(SessionCommand.CommandCodeLibrarySubscribe)!
            .Add(SessionCommand.CommandCodeLibraryUnsubscribe)!
            .Build();
        // Transport, seek and play-from-id are only for the app's own notification controller,
        // trusted system controllers (SystemUI, media keys) and car hosts. Any other app that
        // binds to this exported service keeps the minimal set.
        var fullControl = controller != null
            && (controller.IsTrusted
                || controller.PackageName == global::Android.App.Application.Context.PackageName
                || AndroidAutoHostValidator.IsCarHostPackage(controller.PackageName));
        if (!fullControl)
        {
            var minimal = new PlayerCommands.Builder()
                .Add(BasePlayer.InterfaceConsts.CommandPlayPause)!
                .Add(BasePlayer.InterfaceConsts.CommandGetMetadata)!
                .Build();
            return GuavaFutures.Immediate(MediaSession.ConnectionResult.Accept(sessionCommands, minimal)!);
        }

        // The Media3 notification controller hides the notification when the
        // timeline command is missing, because an empty timeline means "nothing to show".
        var playerCommands = new PlayerCommands.Builder()
            .Add(BasePlayer.InterfaceConsts.CommandPlayPause)!
            .Add(BasePlayer.InterfaceConsts.CommandPrepare)!
            .Add(BasePlayer.InterfaceConsts.CommandStop)!
            .Add(BasePlayer.InterfaceConsts.CommandGetCurrentMediaItem)!
            .Add(BasePlayer.InterfaceConsts.CommandGetMetadata)!
            .Add(BasePlayer.InterfaceConsts.CommandGetTimeline)!
            .Add(BasePlayer.InterfaceConsts.CommandSeekInCurrentMediaItem)!
            .Add(BasePlayer.InterfaceConsts.CommandSeekForward)!
            .Add(BasePlayer.InterfaceConsts.CommandSeekBack)!
            .Add(BasePlayer.InterfaceConsts.CommandSeekToNext)!
            .Add(BasePlayer.InterfaceConsts.CommandSeekToPrevious)!
            .Add(BasePlayer.InterfaceConsts.CommandSetMediaItem)!
            .Build();
        return GuavaFutures.Immediate(MediaSession.ConnectionResult.Accept(sessionCommands, playerCommands)!);
    }

    public void OnPostConnect(MediaSession? session, MediaSession.ControllerInfo? controller)
    {
        _ = session;
        _ = controller;
    }

    public void OnDisconnected(MediaSession? session, MediaSession.ControllerInfo? controller)
    {
        _ = session;
        _ = controller;
    }

    public IListenableFuture? OnGetLibraryRoot(
        MediaLibraryService.MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        MediaLibraryService.LibraryParams? libraryParams)
    {
        _ = session;
        _ = libraryParams;
        if (!AndroidAutoHostValidator.IsCarHostPackage(browser?.PackageName))
        {
            return GuavaFutures.Immediate(LibraryResult.OfError(LibraryResult.ResultErrorPermissionDenied)!);
        }

        var root = new MediaItem.Builder()
            .SetMediaId(Media3LibraryCatalog.RootId)!
            .SetMediaMetadata(new MediaMetadata.Builder().SetTitle("Bible Alarm")!.SetIsBrowsable(Java.Lang.Boolean.True)!.Build())!
            .Build();
        return GuavaFutures.Immediate(LibraryResult.OfItem(root, libraryParams)!);
    }

    public IListenableFuture? OnGetChildren(
        MediaLibraryService.MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        string? parentId,
        int page,
        int pageSize,
        MediaLibraryService.LibraryParams? libraryParams)
    {
        _ = page;
        _ = pageSize;
        return LoadList(session, () =>
        {
            if (!AndroidAutoHostValidator.IsCarHostPackage(browser?.PackageName))
            {
                return LibraryResult.OfError(LibraryResult.ResultErrorPermissionDenied);
            }

            _ = session;
            var children = Media3LibraryCatalog.LoadChildren(global::Android.App.Application.Context);
            return LibraryResult.OfItemList(children, libraryParams);
        });
    }

    public IListenableFuture? OnGetItem(
        MediaLibraryService.MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        string? mediaId)
    {
        _ = browser;
        var item = Media3LibraryCatalog.Find(mediaId, global::Android.App.Application.Context);
        var result = item == null
            ? LibraryResult.OfError(LibraryResult.ResultErrorBadValue)
            : LibraryResult.OfItem(item, null);
        return GuavaFutures.Immediate(result!);
    }

    public IListenableFuture? OnSearch(
        MediaLibraryService.MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        string? query,
        MediaLibraryService.LibraryParams? libraryParams)
    {
        _ = session;
        _ = browser;
        _ = query;
        return GuavaFutures.Immediate(LibraryResult.OfVoid(libraryParams)!);
    }

    public IListenableFuture? OnGetSearchResult(
        MediaLibraryService.MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        string? query,
        int page,
        int pageSize,
        MediaLibraryService.LibraryParams? libraryParams)
    {
        _ = session;
        _ = browser;
        _ = query;
        _ = page;
        _ = pageSize;
        return GuavaFutures.Immediate(LibraryResult.OfItemList(new System.Collections.Generic.List<MediaItem>(), libraryParams)!);
    }

    public IListenableFuture? OnSubscribe(
        MediaLibraryService.MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        string? parentId,
        MediaLibraryService.LibraryParams? libraryParams)
    {
        _ = session;
        _ = browser;
        _ = parentId;
        return GuavaFutures.Immediate(LibraryResult.OfVoid(libraryParams)!);
    }

    public IListenableFuture? OnUnsubscribe(
        MediaLibraryService.MediaLibrarySession? session,
        MediaSession.ControllerInfo? controller,
        string? parentId)
    {
        _ = session;
        _ = controller;
        _ = parentId;
        return GuavaFutures.Immediate(LibraryResult.OfVoid()!);
    }

    public IListenableFuture? OnAddMediaItems(MediaSession? mediaSession, MediaSession.ControllerInfo? controller, System.Collections.Generic.IList<MediaItem>? mediaItems)
    {
        _ = mediaSession;
        _ = controller;
        return GuavaFutures.Immediate(ToJavaList(mediaItems));
    }

    public IListenableFuture? OnSetMediaItems(
        MediaSession? mediaSession,
        MediaSession.ControllerInfo? controller,
        System.Collections.Generic.IList<MediaItem>? mediaItems,
        int startIndex,
        long startPositionMs)
    {
        _ = mediaSession;
        _ = controller;
        _ = startIndex;
        _ = startPositionMs;
        return GuavaFutures.Immediate(ToJavaList(mediaItems));
    }

    public IListenableFuture? OnCustomCommand(MediaSession? session, MediaSession.ControllerInfo? controller, SessionCommand? customCommand, Bundle? args)
    {
        _ = session;
        _ = controller;
        _ = customCommand;
        _ = args;
        return GuavaFutures.ImmediateVoid();
    }

    public IListenableFuture? OnCustomCommand(
        MediaSession? session,
        MediaSession.ControllerInfo? controller,
        SessionCommand? customCommand,
        Bundle? args,
        MediaSession.IProgressReporter? progressReporter)
    {
        _ = progressReporter;
        return OnCustomCommand(session, controller, customCommand, args);
    }

    public IListenableFuture? OnPlaybackResumption(MediaSession? mediaSession, MediaSession.ControllerInfo? controller)
    {
        _ = mediaSession;
        _ = controller;
        return GuavaFutures.Immediate(new ArrayList());
    }

    public IListenableFuture? OnPlaybackResumption(MediaSession? mediaSession, MediaSession.ControllerInfo? controller, bool playWhenReady)
    {
        _ = playWhenReady;
        return OnPlaybackResumption(mediaSession, controller);
    }

    public IListenableFuture? OnSetRating(MediaSession? session, MediaSession.ControllerInfo? controller, Rating? rating)
    {
        _ = session;
        _ = controller;
        _ = rating;
        return GuavaFutures.ImmediateVoid();
    }

    public IListenableFuture? OnSetRating(MediaSession? session, MediaSession.ControllerInfo? controller, string? mediaId, Rating? rating)
    {
        _ = mediaId;
        return OnSetRating(session, controller, rating);
    }

    public void OnPlayerInteractionFinished(MediaSession? session, MediaSession.ControllerInfo? controllerInfo, PlayerCommands? playerCommands)
    {
        _ = session;
        _ = controllerInfo;
        _ = playerCommands;
    }

    private static IListenableFuture LoadList(MediaLibraryService.MediaLibrarySession? session, Func<LibraryResult> load)
    {
        var future = GuavaFutures.CreateSettable();
        _ = Task.Run(async () =>
        {
            try
            {
                await MauiProgram.WaitForBootstrapAsync();
                GuavaFutures.SetResult(future, load()!);
            }
            catch (System.Exception)
            {
                _ = session;
                GuavaFutures.SetResult(future, LibraryResult.OfItemList(new System.Collections.Generic.List<MediaItem>(), null)!);
            }
        });
        return future;
    }

    private static ArrayList ToJavaList(System.Collections.Generic.IList<MediaItem>? items)
    {
        var list = new ArrayList();
        if (items == null)
        {
            return list;
        }

        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }
}
