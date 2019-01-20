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
            var device = AVCaptureDevice.GetDefaultDevice(
                AVCaptureDeviceType.BuiltInWideAngleCamera, AVMediaType.Video, position);
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
    }
}