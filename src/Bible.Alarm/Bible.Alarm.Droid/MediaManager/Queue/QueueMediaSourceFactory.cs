﻿using System;
using Android.Runtime;
using Android.Support.V4.Media;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using Com.Google.Android.Exoplayer2.Source;
using MediaManager.Platforms.Android.Media;

namespace MediaManager.Platforms.Android.Queue
{
    public class QueueMediaSourceFactory : Java.Lang.Object, TimelineQueueEditor.IMediaSourceFactory
    {
        protected MediaManagerImplementation MediaManager => (MediaManagerImplementation)CrossMediaManager.Current;

        public QueueMediaSourceFactory()
        {
        }

        protected QueueMediaSourceFactory(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer)
        {
        }

        public IMediaSource CreateMediaSource(MediaDescriptionCompat description)
        {
            //TODO: We should be able to know the exact type here
            var mediaItem = description.ToMediaItem();
            var fileName = MediaManager.Extractor.GetFileName(mediaItem.MediaUri);
            var fileExtension = MediaManager.Extractor.GetFileExtension(fileName);
            var mediaType = MediaManager.Extractor.GetMediaType(fileExtension);
            return description?.ToMediaSource(mediaType);
        }
    }
}
