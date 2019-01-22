using System.Diagnostics;
using System.Threading.Tasks;
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

        public IPreview Preview { get; private set; }

        public async Task OpenAsync()
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