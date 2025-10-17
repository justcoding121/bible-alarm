using Android.App.Job;
using Android.Content;
using Android.OS;

namespace Bible.Alarm.Services.Droid.Helpers
{
    public static class JobSchedulerHelper
    {
        public static JobInfo.Builder CreateJobBuilderUsingJobId<T>(this Context context, int jobId, int intervalMinutes) where T : JobService
        {
            var javaClass = Java.Lang.Class.FromType(typeof(T));
            var componentName = new ComponentName(context, javaClass);
            var builder = new JobInfo.Builder(jobId, componentName);
            builder.SetRequiredNetworkType(NetworkType.Any);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                builder.SetRequiresBatteryNotLow(true);
            }

            builder.SetPeriodic(1000 * 60 * intervalMinutes);

            return builder;
        }
    }
}
