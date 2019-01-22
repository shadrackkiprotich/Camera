using Android.Hardware.Camera2;
using Android.OS;
using Debug = System.Diagnostics.Debug;

namespace Camera.Droid
{
    public class PreviewStateCallback : CameraCaptureSession.StateCallback
    {
        private readonly Handler _backgroundHandler;
        private readonly CaptureRequest _captureRequest;

        public PreviewStateCallback(CaptureRequest captureRequest, Handler backgroundHandler)
        {
            _captureRequest = captureRequest;
            _backgroundHandler = backgroundHandler;
        }

        public override void OnConfigured(CameraCaptureSession session)
        {
            session.SetRepeatingRequest(_captureRequest, new IgnoreListener(), _backgroundHandler);
        }

        public override void OnConfigureFailed(CameraCaptureSession session)
        {
            Debug.WriteLine("Preview capture request failed.");
        }

        private class IgnoreListener : CameraCaptureSession.CaptureCallback
        {
        }
    }
}