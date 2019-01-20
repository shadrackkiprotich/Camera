using System;
using System.IO;
using Android.Hardware.Camera2;
using Android.OS;
using Java.Lang;
using Java.Util.Concurrent;

namespace Camera.Droid
{
    public class Camera : ICamera
    {
        private readonly string _cameraId;
        private readonly Semaphore _cameraOpenCloseLock = new Semaphore(1);
        private readonly Android.Hardware.Camera2.CameraManager _manager;
        private Handler _backgroundHandler;
        private HandlerThread _backgroundThread;
        private CameraDevice _cameraDevice;
        private CameraStateCallback _stateCallback;

        public Camera(Android.Hardware.Camera2.CameraManager manager, string cameraId)
        {
            _manager = manager;
            _cameraId = cameraId;
        }

        public event EventHandler<Stream> PreviewFrameAvailable;

        public void Open()
        {
            StartBackgroundThread();
            _stateCallback = new CameraStateCallback();
            _stateCallback.Opened += OnOpened;
            LockCameraOpening();
            _manager.OpenCamera(_cameraId, _stateCallback, _backgroundHandler);
        }

        public void Close()
        {
            try
            {
                _cameraOpenCloseLock.Acquire();
                _cameraDevice?.Close();
                StopBackgroundThread();
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
            if (_stateCallback != null) _stateCallback.Opened -= OnOpened;
        }

        // Starts a background thread and its {@link Handler}.
        private void StartBackgroundThread()
        {
            _backgroundThread = new HandlerThread("CameraBackground");
            _backgroundThread.Start();
            _backgroundHandler = new Handler(_backgroundThread.Looper);
        }

        // Stops the background thread and its {@link Handler}.
        private void StopBackgroundThread()
        {
            _backgroundThread.QuitSafely();
            try
            {
                _backgroundThread.Join();
                _backgroundThread = null;
                _backgroundHandler = null;
            }
            catch (InterruptedException e)
            {
                e.PrintStackTrace();
            }
        }
    }
}