using System;
using System.Threading.Tasks;
using Android.Hardware.Camera2;
using Java.Lang;
using Java.Util.Concurrent;

namespace Camera.Droid
{
    public class Camera : ICamera
    {
        private readonly CameraBackgroundThread _backgroundThread = new CameraBackgroundThread();
        private readonly string _cameraId;
        private readonly Semaphore _cameraOpenCloseLock = new Semaphore(1);
        private readonly Android.Hardware.Camera2.CameraManager _manager;
        private AsyncAutoResetEvent _asyncAutoResetEvent;
        private CameraDevice _cameraDevice;
        private CameraStateCallback _stateCallback;

        public Camera(Android.Hardware.Camera2.CameraManager manager, string cameraId)
        {
            _manager = manager;
            _cameraId = cameraId;
        }

        public IPreview Preview { get; private set; }

        public async Task OpenAsync()
        {
            _backgroundThread.Start();
            _stateCallback = new CameraStateCallback();
            _stateCallback.Opened += OnOpened;
            LockCameraOpening();
            _asyncAutoResetEvent = new AsyncAutoResetEvent(false);
            _manager.OpenCamera(_cameraId, _stateCallback, _backgroundThread.Handler);
            await _asyncAutoResetEvent.WaitAsync(TimeSpan.FromSeconds(3));
        }

        public byte[] TakePicture()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            try
            {
                _cameraOpenCloseLock.Acquire();
                _cameraDevice?.Close();
                _backgroundThread.Stop();
            }
            catch (InterruptedException e)
            {
                throw new RuntimeException("Interrupted while trying to lock camera closing.", e);
            }
            finally
            {
                _cameraOpenCloseLock.Release();
            }
        }

        private void LockCameraOpening()
        {
            try
            {
                if (!_cameraOpenCloseLock.TryAcquire(2500, TimeUnit.Milliseconds))
                    throw new ApplicationException("Time out waiting to lock camera opening.");
            }
            catch (InterruptedException e)
            {
                throw new ApplicationException("Interrupted while trying to lock camera opening.", e);
            }
        }

        private void OnOpened(object sender, CameraDevice device)
        {
            _cameraDevice = device;
            Preview = new Preview(_cameraDevice, _manager, _backgroundThread.Handler);
            if (_stateCallback != null) _stateCallback.Opened -= OnOpened;
            _asyncAutoResetEvent.Set();
            _asyncAutoResetEvent = null;
        }
    }
}