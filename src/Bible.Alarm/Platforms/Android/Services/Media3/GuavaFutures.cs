using Google.Common.Util.Concurrent;
using Java.Interop;
using JniObject = Java.Lang.Object;

namespace Bible.Alarm.Platforms.Android.Services.Media3;

/// <summary>
/// The Guava C# binding exposes <see cref="IListenableFuture"/> and not <c>Futures</c> or <c>SettableFuture</c>.
/// Those Java types are still in the Media3 dependency, so completed futures are created through JNI.
/// </summary>
internal static class GuavaFutures
{
    public static IListenableFuture ImmediateVoid()
    {
        var futures = Java.Lang.Class.ForName("com.google.common.util.concurrent.Futures")
            ?? throw new InvalidOperationException("Guava Futures is not on the classpath.");
        var method = futures.GetMethod("immediateVoidFuture", []);
        var value = method?.Invoke(null) as JniObject;
        return value?.JavaCast<IListenableFuture>()
            ?? throw new InvalidOperationException("immediateVoidFuture returned null.");
    }

    public static IListenableFuture Immediate(JniObject value)
    {
        var futures = Java.Lang.Class.ForName("com.google.common.util.concurrent.Futures")
            ?? throw new InvalidOperationException("Guava Futures is not on the classpath.");
        var method = futures.GetMethod("immediateFuture", Java.Lang.Class.FromType(typeof(JniObject)));
        var result = method?.Invoke(null, value) as JniObject;
        return result?.JavaCast<IListenableFuture>()
            ?? throw new InvalidOperationException("immediateFuture returned null.");
    }

    public static IListenableFuture CreateSettable()
    {
        var settable = Java.Lang.Class.ForName("com.google.common.util.concurrent.SettableFuture")
            ?? throw new InvalidOperationException("Guava SettableFuture is not on the classpath.");
        var method = settable.GetMethod("create", []);
        var value = method?.Invoke(null) as JniObject;
        return value?.JavaCast<IListenableFuture>()
            ?? throw new InvalidOperationException("SettableFuture.create returned null.");
    }

    public static void SetResult(IListenableFuture future, JniObject value)
    {
        var settable = Java.Lang.Class.ForName("com.google.common.util.concurrent.SettableFuture")
            ?? throw new InvalidOperationException("Guava SettableFuture is not on the classpath.");
        var method = settable.GetMethod("set", Java.Lang.Class.FromType(typeof(JniObject)));
        method?.Invoke(future as JniObject, value);
    }
}
