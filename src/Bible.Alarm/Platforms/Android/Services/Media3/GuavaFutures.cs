#nullable enable

using Google.Common.Util.Concurrent;
using Java.Interop;
using JniObject = Java.Lang.Object;

namespace Bible.Alarm.Platforms.Android.Services.Media3;

/// <summary>
/// The Guava C# binding exposes <see cref="IListenableFuture"/> and not <c>Futures</c> or <c>SettableFuture</c>.
/// Those Java types are still in the Media3 dependency. A bare <c>Class.forName</c> on a worker thread uses the
/// system loader and cannot see them, so lookups go through the application class loader.
/// </summary>
internal static class GuavaFutures
{
    public static IListenableFuture ImmediateVoid()
    {
        var futures = Require("com.google.common.util.concurrent.Futures");
        var method = futures.GetMethod("immediateVoidFuture");
        var value = method?.Invoke(null);
        return value?.JavaCast<IListenableFuture>()
            ?? throw new InvalidOperationException("immediateVoidFuture returned null.");
    }

    public static IListenableFuture Immediate(JniObject value)
    {
        var futures = Require("com.google.common.util.concurrent.Futures");
        var method = futures.GetMethod("immediateFuture", Java.Lang.Class.FromType(typeof(JniObject)));
        var result = method?.Invoke(null, value);
        return result?.JavaCast<IListenableFuture>()
            ?? throw new InvalidOperationException("immediateFuture returned null.");
    }

    public static IListenableFuture CreateSettable()
    {
        var settable = Require("com.google.common.util.concurrent.SettableFuture");
        var method = settable.GetMethod("create");
        var value = method?.Invoke(null);
        return value?.JavaCast<IListenableFuture>()
            ?? throw new InvalidOperationException("SettableFuture.create returned null.");
    }

    /// <summary>
    /// Completes a settable future. Returns false when the Java <c>set</c> method is missing or returns false.
    /// </summary>
    public static bool SetResult(IListenableFuture future, JniObject value)
    {
        // The instance is already loaded. Looking the class up again by name fails on the browse worker thread.
        var instance = future as JniObject;
        var method = instance?.Class?.GetMethod("set", Java.Lang.Class.FromType(typeof(JniObject)));
        if (method == null || instance == null)
        {
            return false;
        }

        var invoked = method.Invoke(instance, value);
        if (invoked is Java.Lang.Boolean boolean)
        {
            return boolean.BooleanValue();
        }

        return invoked != null;
    }

    private static Java.Lang.Class Require(string className)
    {
        var loader = global::Android.App.Application.Context?.ClassLoader;
        var type = loader == null
            ? Java.Lang.Class.ForName(className)
            : Java.Lang.Class.ForName(className, true, loader);
        return type ?? throw new InvalidOperationException(className + " is not on the classpath.");
    }
}
