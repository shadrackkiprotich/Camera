using System;
using AVFoundation;
using Camera.Internals;

namespace Camera.iOS
{
    public class CameraManager : ICameraManager
    {
        public ICamera GetCamera(LogicalCameras camera)
        {
            var position = ToPosition(camera);
            var device = GetCameraForOrientation(position);
            return new Camera(device);
        }

        private static AVCaptureDevicePosition ToPosition(LogicalCameras camera)
        {
            switch (camera)
            {
                case LogicalCameras.Front:
                    return AVCaptureDevicePosition.Front;
                case LogicalCameras.Rear:
                    return AVCaptureDevicePosition.Back;
                default:
                    throw new NotSupportedException("NotSupported");
            }
        }

        private static AVCaptureDevice GetCameraForOrientation(AVCaptureDevicePosition orientation)
        {
            var devices = AVCaptureDevice.DevicesWithMediaType(AVMediaType.Video);

            foreach (var device in devices)
                if (device.Position == orientation)
                    return device;
            return null;
        }
    }
}