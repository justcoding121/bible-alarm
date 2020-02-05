﻿using Bible.Alarm.UITests.Helpers;
using NUnit.Framework;
using System;
using Xamarin.UITest;

namespace Bible.Alarm.UITests
{
    [TestFixture(Platform.Android)]
    //[TestFixture(Platform.iOS)]
    public class Tests
    {
        IApp app;
        Platform platform;

        public Tests(Platform platform)
        {
            this.platform = platform;
        }

        [SetUp]
        public void BeforeEachTest()
        {
            app = AppInitializer.StartApp(platform);
        }

        [Test]
        public void Smoke_Test_Alarm()
        {
            app.WaitForElement(c => c.Marked("HomePage"),
            "Took more than 30 seconds to show home page.", TimeSpan.FromSeconds(30));

            app.WaitForElement(c => c.Marked("AddScheduleButton"));

            app.Screenshot("Home page.");

            app.Tap(x => x.Marked("AddScheduleButton"));
            app.WaitForElement(c => c.Marked("SchedulePage"),
            "Took more than 30 seconds to show add schedule page.", TimeSpan.FromSeconds(30));

            app.WaitForElement(c => c.Marked("CancelButton"));
            app.WaitForElement(c => c.Marked("SaveButton"));

            var deviceTime = DateTime.Parse((string)app.Invoke("GetDeviceTime"));
            app.UpdateTimePicker(this.platform, deviceTime.AddMinutes(2));

            app.Screenshot("Schedule page.");

            app.Tap(x => x.Marked("SaveButton"));
            app.WaitForElement(c => c.Marked("HomePage"),
            "Took more than 30 seconds to show home page after save.", TimeSpan.FromSeconds(30));

            app.WaitForElement(c => c.Marked("Dismiss Alarm"), "Alarm did not trigger in time.", TimeSpan.FromMinutes(5));

            var isAlarmOn = bool.Parse((string)app.Invoke("IsAlarmOn"));

            Assert.IsTrue(isAlarmOn, "Alarm is not On.");

            app.Screenshot("Alarm modal.");

            app.Tap(x => x.Marked("Dismiss Alarm"));

            app.WaitForElement(c => c.Marked("HomePage"),
           "Took more than 30 seconds to show home page after alarm dismissal.", TimeSpan.FromSeconds(30));

            isAlarmOn = bool.Parse((string)app.Invoke("IsAlarmOn"));
            Assert.IsFalse(isAlarmOn, "Alarm is not off after dismissal.");
        }
    }
}
