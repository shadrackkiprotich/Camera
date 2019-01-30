using System;
using Android.Hardware.Camera2;

namespace Camera.Droid
{
    public class CameraCaptureListener : CameraCaptureSession.CaptureCallback
    {
        public event EventHandler<CaptureResult> CaptureResultAvailable;

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request,
            TotalCaptureResult result)
        {
            CaptureResultAvailable?.Invoke(this, result);
        }

        public override void OnCaptureProgressed(CameraCaptureSession session, CaptureRequest request,
            CaptureResult partialResult)
        {
            CaptureResultAvailable?.Invoke(this, partialResult);
        }
    }
}