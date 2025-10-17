using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Cast.Framework;
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
using Bible.Alarm.Services.Droid.Extensions;
using Bible.Alarm.Services.Infrastructure;
using Java.Interop;
using MediaManager;
using Newtonsoft.Json;
using NLog;
using Plugin.CurrentActivity;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Controls.Compatibility;

namespace Bible.Alarm.Droid
{
    [Activity(Label = "Bible Alarm", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : global::Android.App.Activity
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

        private IContainer _container;
        private CastContext _castContext;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // MAUI initialization is handled automatically

            // Initialize MediaManager
            CrossMediaManager.Current.Init(this);

            // Initialize Cast Framework
            _castContext = CastContext.GetSharedInstance(this);

            // Initialize container
            _container = BootstrapHelper.InitializeUi(Logger, this, this.Application);

            // Initialize MAUI application
            var app = new App(_container);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
        {
            // MAUI handles permissions automatically
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        protected override void OnResume()
        {
            base.OnResume();
            CrossMediaManager.Current.MediaPlayer.Play();
        }

        protected override void OnPause()
        {
            base.OnPause();
            CrossMediaManager.Current.MediaPlayer.Pause();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            BootstrapHelper.Remove(this.Application);
        }
    }
}
