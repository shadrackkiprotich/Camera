using Android.Content;
using Android.Hardware.Camera2;
using Camera.Internals;
using Java.Lang;
using Xamarin.Forms;
using Application = Android.App.Application;
using CameraManager = Camera.Droid.CameraManager;

[assembly: Dependency(typeof(CameraManager))]

namespace Camera.Droid
{
    public class CameraManager : ICameraManager
    {
        private readonly Context _context;

        public CameraManager()
        {
            _context = Application.Context;
        }

        public ICamera GetCamera(LogicalCameras camera)
        {
            var manager = GetManager();
            var cameraId = GetCameraId(camera, manager);
            return new Camera(manager, cameraId);
        }

        private static string GetCameraId(LogicalCameras camera, Android.Hardware.Camera2.CameraManager manager)
        {
            var cameraIdList = manager.GetCameraIdList();
            var lensFacing = ToLensFacing(camera);
            foreach (var cameraId in cameraIdList)
            {
                var cameraCharacteristics = manager.GetCameraCharacteristics(cameraId);
                var facing = (Integer) cameraCharacteristics.Get(CameraCharacteristics.LensFacing);
                if (facing != null && facing == lensFacing) return cameraId;
            }

            throw new UnsupportedOperationException(GetUnsupportedCameraMessage(camera));
        }

        private static Integer ToLensFacing(LogicalCameras camera)
        {
            switch (camera)
            {
                case LogicalCameras.Front:
                    return Integer.ValueOf((int) LensFacing.Front);
                case LogicalCameras.Rear:
                    return Integer.ValueOf((int) LensFacing.Back);
                default:
                    throw new UnsupportedOperationException(GetUnsupportedCameraMessage(camera));
            }
        }

        private static string GetUnsupportedCameraMessage(LogicalCameras camera)
        {
            return "The camera " + camera + " is not supported yet.";
        }

        private Android.Hardware.Camera2.CameraManager GetManager()
        {
            var cameraManager = (Android.Hardware.Camera2.CameraManager)
                _context.GetSystemService(Context.CameraService);
            if (cameraManager == null)
                throw new UnsupportedOperationException("This device does not support the camera2 API.");

            return cameraManager;
        }
    }
}