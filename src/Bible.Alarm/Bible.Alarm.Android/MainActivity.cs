﻿using System;
using Android.App;
using Android.Content.PM;
using Android.OS;
using System.Threading.Tasks;
using JW.Alarm.Services.Droid.Helpers;
using MediaManager;
using Android.Content;
using JW.Alarm.Common.Mvvm;
using Bible.Alarm.Services.Infrastructure;
using NLog;

namespace Bible.Alarm.Droid
{
    [Activity(Label = "Bible Alarm", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public MainActivity() : base()
        {
            LogSetup.Initialize();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            logger.Info("Android App started.");

            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(savedInstanceState);

            try
            {
                if (IocSetup.Container == null)
                {
                    IocSetup.Initialize();
                    IocSetup.Container.Resolve<IMediaManager>().SetContext(this);
                }

                global::Xamarin.Forms.Forms.Init(this, savedInstanceState);
                LoadApplication(new App());
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Android Application Crashed.");
                throw;
            }

            Task.Run(async () =>
            {
                try
                {
                    await BootstrapHelper.VerifyMediaLookUpService();
                    await BootstrapHelper.InitializeDatabase();
                    await Messenger<bool>.Publish(Messages.Initialized, true);
                }
                catch (Exception e)
                {
                    logger.Fatal(e, "Android Initialization Crashed.");
                }
            });
        }

        protected override void OnStart()
        {
            base.OnStart();

            try
            {
                Intent intent = new Intent();
                intent.SetAction("com.Bible.Alarm.Restart");
                SendBroadcast(intent);
            }
            catch (Exception e)
            {
                logger.Fatal(e, "Failed to start restart service.");
            }
        }

    }
}