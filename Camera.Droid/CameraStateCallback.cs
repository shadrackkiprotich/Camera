using System;
using System.Diagnostics;
using Android.Hardware.Camera2;

namespace Camera.Droid
{
    internal class CameraStateCallback : CameraDevice.StateCallback
    {
        public CameraDevice Camera { get; private set; }

        public event EventHandler<CameraDevice> Opened;

        public override void OnDisconnected(CameraDevice camera)
        {
            Debug.WriteLine("Camera disconnected.");
            camera.Close();
            Camera = null;
        }

        public override void OnError(CameraDevice camera, CameraError error)
        {
            Debug.WriteLine("Camera error: " + error);
            camera.Close();
            Camera = null;
        }

        public override void OnOpened(CameraDevice camera)
        {
            Opened?.Invoke(this, camera);
            Camera = camera;
            Debug.WriteLine("Camera opened");
        }
    }
}