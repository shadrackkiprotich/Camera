using System;
using Android.Hardware.Camera2;

namespace Camera.Droid
{
    public class StateCallback : CameraCaptureSession.StateCallback
    {
        public event EventHandler<CameraCaptureSession> Configured;
        public event EventHandler<CameraCaptureSession> ConfigureFailed;

        public override void OnConfigured(CameraCaptureSession session)
        {
            Configured?.Invoke(this, session);
        }

        public override void OnConfigureFailed(CameraCaptureSession session)
        {
            ConfigureFailed?.Invoke(this, session);
        }
    }
}