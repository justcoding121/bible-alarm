using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Contracts.Media;
using Bible.Alarm.Droid.Services.Handlers;
using Bible.Alarm.Droid.Services.Platform;
using Bible.Alarm.Services.Droid;
using Bible.Alarm.Services.Droid.Helpers;
using Bible.Alarm.Services.Droid.Tasks;
// using Bible.Alarm.Services.Droid.Extensions; // Removed - no longer needed
using Bible.Alarm.Services.Infrastructure;
using Java.Interop;
using Newtonsoft.Json;
using Serilog;
using Plugin.CurrentActivity;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Controls.Compatibility;

namespace Bible.Alarm.Droid
{
    [Activity(Label = "Bible Alarm", Icon = "@mipmap/ic_launcher", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : MauiAppCompatActivity
    {
        private static readonly ILogger Logger = Log.ForContext<MainActivity>();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // MediaManager removed - using MediaElement instead

            // Cast Framework removed - using MediaElement instead

            // MAUI handles dependency injection through MauiProgram
            // ServiceProviderManager is initialized in MauiProgram
            
            // Initialize platform-specific services
            BootstrapHelper.InitializeUi(Logger, this, Application);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
        {
            // MAUI handles permissions automatically
#pragma warning disable CA1416
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
#pragma warning restore CA1416
        }

        protected override void OnResume()
        {
            base.OnResume();
            // MediaManager removed - using MediaElement instead
        }

        protected override void OnPause()
        {
            base.OnPause();
            // MediaManager removed - using MediaElement instead
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            BootstrapHelper.Remove(Application);
        }
    }
}
