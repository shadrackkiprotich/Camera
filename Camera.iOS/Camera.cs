using System;
using System.Diagnostics;
using System.IO;
using AVFoundation;

namespace Camera.iOS
{
    public class Camera : ICamera
    {
        private readonly AVCaptureDevice _camera;
        private readonly AVCaptureSession _session;

        public Camera(AVCaptureDevice camera)
        {
            _camera = camera;
            _session = new AVCaptureSession();
            InitializeSessionInput();
        }

        public event EventHandler<Stream> PreviewFrameAvailable;

        public void Open()
        {
            _session.StartRunning();
        }

        public void Close()
        {
            _session.StopRunning();
        }

        private void InitializeSessionInput()
        {
            var input = new AVCaptureDeviceInput(_camera, out var error);
            Debug.WriteLine(error);
            _session.AddInput(input);
        }
    }
}