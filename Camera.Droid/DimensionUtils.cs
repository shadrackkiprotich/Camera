using Android.App;
using Android.Util;
using Java.Lang;

namespace Camera.Droid
{
    public static class DimensionUtils
    {
        /// <summary>
        ///     This method converts dp unit to equivalent pixels, depending on device density.
        /// </summary>
        /// <param name="dp">A value in dp (density independent pixels) unit. Which we need to convert into pixels</param>
        /// <returns>A float value to represent px equivalent to dp depending on device density</returns>
        public static int ConvertDpToPixel(float dp)
        {
            var context = Application.Context;
            const float defaultDensity = (float) DisplayMetricsDensity.Default;
            return Math.Round(dp * ((float) context.Resources.DisplayMetrics.DensityDpi / defaultDensity));
        }

        /// <summary>
        ///     This method converts device specific pixels to density independent pixels.
        /// </summary>
        /// <param name="px">A value in px (pixels) unit. Which we need to convert into db</param>
        /// <returns>A float value to represent dp equivalent to px value</returns>
        public static int ConvertPixelsToDp(float px)
        {
            var context = Application.Context;
            const float defaultDensity = (float) DisplayMetricsDensity.Default;
            return Math.Round(px / ((float) context.Resources.DisplayMetrics.DensityDpi / defaultDensity));
        }

        public static Size ToAndroidSize(Xamarin.Forms.Size dpSize)
        {
            var (width, height) = dpSize;
            var widthPx = ConvertDpToPixel((float) width);
            var heightPx = ConvertDpToPixel((float) height);
            return new Size(widthPx, heightPx);
        }

        public static Xamarin.Forms.Size ToXamarinFormsSize(Size pxSize)
        {
            var widthPx = ConvertPixelsToDp(pxSize.Width);
            var heightPx = ConvertPixelsToDp(pxSize.Height);
            return new Xamarin.Forms.Size(widthPx, heightPx);
        }
    }
}