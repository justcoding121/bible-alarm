using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Android.Views;
using Android.Widget;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using Java.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Com.Google.Android.Exoplayer2.Ext.Mediasession.MediaSessionConnector;

namespace MediaManager.Platforms.Android.Player
{
    public class MetaDataProvider : Java.Lang.Object, IMediaMetadataProvider
    {
        private DefaultMediaMetadataProvider defaultMediaMetadataProvider;

        public MetaDataProvider(
            MediaControllerCompat mediaController, String metadataExtrasPrefix)
        {
            defaultMediaMetadataProvider =
                    new DefaultMediaMetadataProvider(mediaController, metadataExtrasPrefix);
        }

        public MediaMetadataCompat GetMetadata(IPlayer player)
        {
            var mediaMetadata = defaultMediaMetadataProvider.GetMetadata(player);

            var builder = new MediaMetadataCompat.Builder();

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyTitle))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyTitle, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyTitle));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyArtist) && !string.IsNullOrWhiteSpace(mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyArtist)))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyArtist, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyArtist));
            }
            else
            {
                if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyDisplaySubtitle))
                {
                    builder.PutString(MediaMetadataCompat.MetadataKeyArtist, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyDisplaySubtitle));
                }
                else
                {
                    //hard-coded here
                    //TODO: Use another non-null metadata here
                    builder.PutString(MediaMetadataCompat.MetadataKeyArtist, "Bible Alarm");
                }
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyMediaUri))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyMediaUri, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyMediaUri));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyAdvertisement))
            {
                builder.PutLong(MediaMetadataCompat.MetadataKeyAdvertisement, mediaMetadata.GetLong(MediaMetadataCompat.MetadataKeyAdvertisement));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyAlbum))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyAlbum, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyAlbum));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyAlbumArt))
            {
                builder.PutBitmap(MediaMetadataCompat.MetadataKeyAlbumArt, mediaMetadata.GetBitmap(MediaMetadataCompat.MetadataKeyAlbumArt));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyAlbumArtist))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyAlbumArtist, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyAlbumArtist));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyAlbumArtUri))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyAlbumArtUri, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyAlbumArtUri));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyArt))
            {
                builder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, mediaMetadata.GetBitmap(MediaMetadataCompat.MetadataKeyArt));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyArtUri))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyArtUri, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyArtUri));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyAuthor))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyAuthor, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyAuthor));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyCompilation))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyCompilation, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyCompilation));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyComposer))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyComposer, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyComposer));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyRating))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyRating, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyRating));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyDiscNumber))
            {
                builder.PutLong(MediaMetadataCompat.MetadataKeyDiscNumber, mediaMetadata.GetLong(MediaMetadataCompat.MetadataKeyDiscNumber));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyDisplayDescription))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyDisplayDescription, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyDisplayDescription));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyDisplayIcon))
            {
                builder.PutBitmap(MediaMetadataCompat.MetadataKeyDisplayIcon, mediaMetadata.GetBitmap(MediaMetadataCompat.MetadataKeyDisplayIcon));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyDisplayIconUri))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyDisplayIconUri, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyDisplayIconUri));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyDisplaySubtitle))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyDisplaySubtitle, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyDisplaySubtitle));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyDisplayTitle))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyDisplayTitle, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyDisplayTitle));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyDownloadStatus))
            {
                builder.PutLong(MediaMetadataCompat.MetadataKeyDownloadStatus, mediaMetadata.GetLong(MediaMetadataCompat.MetadataKeyDownloadStatus));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyDuration))
            {
                builder.PutLong(MediaMetadataCompat.MetadataKeyDuration, mediaMetadata.GetLong(MediaMetadataCompat.MetadataKeyDuration));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyGenre))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyGenre, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyGenre));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyMediaId))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyMediaId, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyMediaId));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyMediaUri))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyMediaUri, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyMediaUri));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyNumTracks))
            {
                builder.PutLong(MediaMetadataCompat.MetadataKeyNumTracks, mediaMetadata.GetLong(MediaMetadataCompat.MetadataKeyNumTracks));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyRating))
            {
                builder.PutRating(MediaMetadataCompat.MetadataKeyRating, mediaMetadata.GetRating(MediaMetadataCompat.MetadataKeyRating));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyTitle))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyTitle, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyTitle));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyTrackNumber))
            {
                builder.PutLong(MediaMetadataCompat.MetadataKeyTrackNumber, mediaMetadata.GetLong(MediaMetadataCompat.MetadataKeyTrackNumber));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyUserRating))
            {
                builder.PutRating(MediaMetadataCompat.MetadataKeyUserRating, mediaMetadata.GetRating(MediaMetadataCompat.MetadataKeyUserRating));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyWriter))
            {
                builder.PutString(MediaMetadataCompat.MetadataKeyWriter, mediaMetadata.GetString(MediaMetadataCompat.MetadataKeyWriter));
            }

            if (mediaMetadata.ContainsKey(MediaMetadataCompat.MetadataKeyYear))
            {
                builder.PutLong(MediaMetadataCompat.MetadataKeyYear, mediaMetadata.GetLong(MediaMetadataCompat.MetadataKeyYear));
            }

            return builder.Build();
        }
    }
}