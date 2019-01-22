using System;
using System.Diagnostics;
using Android.Media;
using Object = Java.Lang.Object;

namespace Camera.Droid
{
    public class ImageAvailableListener : Object, ImageReader.IOnImageAvailableListener
    {
        public void OnImageAvailable(ImageReader reader)
        {
            var image = reader.AcquireLatestImage();
            if (image != null)
                ImageAvailable?.Invoke(this, image);
            else
                Debug.WriteLine("Image is null");
        }

        public event EventHandler<Image> ImageAvailable;
    }
}