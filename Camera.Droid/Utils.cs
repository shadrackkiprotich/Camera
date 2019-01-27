using Android.App;
using Android.Content;
using Android.Hardware.Camera2;
using Android.Util;
using Android.Views;
using Java.Interop;
using Java.Lang;

namespace Camera.Droid
{
    public static class Utils
    {
        private static readonly SparseIntArray Orientations = new SparseIntArray();

        static Utils()
        {
            // fill ORIENTATIONS list
            Orientations.Append((int)SurfaceOrientation.Rotation0, 90);
            Orientations.Append((int)SurfaceOrientation.Rotation90, 0);
            Orientations.Append((int)SurfaceOrientation.Rotation180, 270);
            Orientations.Append((int)SurfaceOrientation.Rotation270, 180);
        }
        
        public static bool IsFlashSupported(Android.Hardware.Camera2.CameraManager cameraManager, string cameraId)
        {
            var characteristics = cameraManager.GetCameraCharacteristics(cameraId);
            // Check if the flash is supported.
            var available = (Boolean) characteristics.Get(CameraCharacteristics.FlashInfoAvailable);
            if (available == null) return false;

            return (bool) available;
        }
        
        public static void SetAutoFlash(CaptureRequest.Builder requestBuilder,
            Android.Hardware.Camera2.CameraManager cameraManager, string cameraId)
        {
            if (IsFlashSupported(cameraManager, cameraId))
            {
                requestBuilder.Set(CaptureRequest.ControlAeMode, (int) ControlAEMode.OnAutoFlash);
            }
        }
        
        public static SurfaceOrientation CalculateRotation()
        {
            var service = Application.Context.GetSystemService(Context.WindowService);
            var display = service?.JavaCast<IWindowManager>()?.DefaultDisplay;
            return display?.Rotation ?? 0;
        }
        
        public static int GetOrientation(SurfaceOrientation rotation)
        {
            return Orientations.Get((int) rotation);
        }
    }
}