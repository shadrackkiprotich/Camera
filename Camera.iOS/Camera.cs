using System.Diagnostics;
using System.Threading.Tasks;
using AVFoundation;
using Xamarin.Forms;

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

        public async Task OpenAsync()
        {
            _session.StartRunning();
        }

        public Task<ICameraPreview> OpenWithPreviewAsync(Size previewRequestSize)
        {
            throw new System.NotImplementedException();
        }

        public Task<byte[]> TakePictureAsync()
        {
            throw new System.NotImplementedException();
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

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }
    }
}