using System.Runtime.Versioning;
using Android.App.Job;
using Android.Content;
using Android.OS;
using Java.Lang;

namespace Bible.Alarm.Platforms.Android.Services.Helpers;

public static class JobSchedulerHelper
{
    public static JobInfo.Builder CreateJobBuilderUsingJobId<T>(this Context context, int jobId, int intervalMinutes)
        where T : JobService
    {
        var javaClass = Class.FromType(typeof(T));
        var componentName = new ComponentName(context, javaClass);
        var builder = new JobInfo.Builder(jobId, componentName);
        builder.SetRequiredNetworkType(NetworkType.Any);

        // Minimum supported is API 26, so this is always available
        builder.SetRequiresBatteryNotLow(true);

        builder.SetPeriodic(1000 * 60 * intervalMinutes);

        return builder;
    }
}
