using Android.App;
using Android.Content;
using Android.Gms.Cast.Framework;
using Android.Gms.Cast.Framework.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Com.Google.Android.Exoplayer2.Ext.Cast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[assembly: MetaData("com.google.android.gms.cast.framework.OPTIONS_PROVIDER_CLASS_NAME",
    Value = "Bible.Alarm.Droid.MediaManager.MediaSession.CastOptionsProvider")]

namespace Bible.Alarm.Droid.MediaManager.MediaSession
{   
    [Register("Bible/Alarm/Droid/MediaManager/MediaSession/CastOptionsProvider")]
    public class CastOptionsProvider : Java.Lang.Object, IOptionsProvider
    {
        public IList<SessionProvider> GetAdditionalSessionProviders(Context appContext)
        {
            return default;
        }

        public CastOptions GetCastOptions(Context appContext)
        {
            return new CastOptions.Builder()
             .SetReceiverApplicationId(DefaultCastOptionsProvider.AppIdDefaultReceiverWithDrm)
             .SetCastMediaOptions(new CastMediaOptions.Builder()
                 .SetMediaSessionEnabled(false)
                 .SetNotificationOptions(null)
                 .Build())
             .SetStopReceiverApplicationWhenEndingSession(true)
             .Build();
        }
    }
}