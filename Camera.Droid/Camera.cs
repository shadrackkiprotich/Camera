using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.Util;
using Android.Views;
using Java.Lang;
using Java.Util;
using Java.Util.Concurrent;

namespace Camera.Droid
{
    public class Camera : ICamera
    {
        public enum CaptureState
        {
            Preview,
            WaitingLock,
            WaitingPrecapture,
            WaitingNonPrecapture,
            PictureTaken
        }

        private readonly CameraBackgroundThread _backgroundThread = new CameraBackgroundThread();
        private readonly string _cameraId;
        private readonly Semaphore _cameraOpenCloseLock = new Semaphore(1);
        private readonly CameraCaptureListener _captureListener = new CameraCaptureListener();
        private readonly Context _context;
        private readonly Android.Hardware.Camera2.CameraManager _manager;
        private readonly CameraStateCallback _stateCallback = new CameraStateCallback();

        private AsyncAutoResetEvent _asyncAutoResetEvent;
        private CameraDevice _cameraDevice;
        private CameraPreview _cameraPreview;
        private CameraCaptureSession _captureSession;
        private bool _hasPreview;
        private ImageAvailableListener _imageAvailableListener;
        private ImageReader _imageReader;
        private Size _imageSize;
        private byte[] _latestImageCapture;
        private CaptureRequest _previewRequest;
        private CaptureRequest.Builder _previewRequestBuilder;
        private CaptureRequest.Builder _stillCaptureBuilder;

        public Camera(Context context, Android.Hardware.Camera2.CameraManager manager, string cameraId)
        {
            _context = context;
            _manager = manager;
            _cameraId = cameraId;
            _stateCallback.Opened += OnOpened;
            _captureListener.CaptureResultAvailable += CaptureListenerOnCaptureResultAvailable;
        }

        private CaptureState State { get; set; }

        public async Task OpenAsync()
        {
            await OpenAsync(false);
        }

        public async Task<ICameraPreview> OpenWithPreviewAsync(Xamarin.Forms.Size previewRequestSize)
        {
            await OpenAsync(true);
            var stateCallback = new StateCallback();
            stateCallback.Configured += SessionConfigured;
            stateCallback.ConfigureFailed += SessionConfigureFailed;
            var previewSurface = _cameraPreview.CreateSurface(previewRequestSize);

            _previewRequestBuilder.AddTarget(previewSurface);
            _previewRequestBuilder.Set(CaptureRequest.ControlAfMode, (int) ControlAFMode.ContinuousPicture);

            var characteristics = _manager.GetCameraCharacteristics(_cameraDevice.Id);
            var map = (StreamConfigurationMap) characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            // ReSharper disable once CoVariantArrayConversion
            _imageSize = (Size) Collections.Max(
                Arrays.AsList(map.GetOutputSizes((int) ImageFormatType.Jpeg)),
                new CompareSizesByArea());

            _imageReader = ImageReader.NewInstance(_imageSize.Width, _imageSize.Height,
                ImageFormatType.Jpeg, /* maxImages */2);
            _imageAvailableListener = new ImageAvailableListener();
            _imageAvailableListener.ImageAvailable += CaptureAvailable;
            _imageReader.SetOnImageAvailableListener(_imageAvailableListener, _backgroundThread.Handler);
            var surfaces = new List<Surface> {previewSurface, _imageReader.Surface};
            _cameraDevice.CreateCaptureSession(surfaces, stateCallback, null);
            return _cameraPreview;
        }

        public async Task<byte[]> TakePictureAsync()
        {
            var asyncAre = new AsyncAutoResetEvent(false);
            LockFocus();
            await asyncAre.WaitAsync(TimeSpan.FromSeconds(3));
            return _latestImageCapture;
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

        public void Dispose()
        {
            _cameraOpenCloseLock?.Dispose();
            _cameraDevice?.Dispose();

            _stateCallback.Opened -= OnOpened;
            _stateCallback?.Dispose();

            _captureListener.CaptureResultAvailable -= CaptureListenerOnCaptureResultAvailable;
            _captureListener?.Dispose();
        }

        private static void SessionConfigureFailed(object sender, CameraCaptureSession e)
        {
            ((StateCallback) sender).ConfigureFailed -= SessionConfigureFailed;
            Debug.WriteLine("Session configure failed.");
        }

        private void SessionConfigured(object sender, CameraCaptureSession e)
        {
            ((StateCallback) sender).Configured -= SessionConfigured;
            _captureSession = e;

            // Auto focus should be continuous for camera preview.
            _previewRequestBuilder.Set(CaptureRequest.ControlAfMode, (int) ControlAFMode.ContinuousPicture);
            // Flash is automatically enabled when necessary.
            Utils.SetAutoFlash(_previewRequestBuilder, _manager, _cameraId);

            e.SetRepeatingRequest(_previewRequest = _previewRequestBuilder.Build(), _captureListener,
                _backgroundThread.Handler);
        }

        private void CaptureAvailable(object sender, Image e)
        {
            var buffer = e.GetPlanes()[0].Buffer;
            _latestImageCapture = new byte[buffer.Remaining()];
            buffer.Get(_latestImageCapture);
            e.Close();
        }

        private async Task OpenAsync(bool hasPreview)
        {
            _hasPreview = hasPreview;
            _backgroundThread.Start();
            LockCameraOpening();
            _asyncAutoResetEvent = new AsyncAutoResetEvent(false);
            _manager.OpenCamera(_cameraId, _stateCallback, _backgroundThread.Handler);
            await _asyncAutoResetEvent.WaitAsync(TimeSpan.FromSeconds(3));
            _previewRequestBuilder = _cameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
        }

        private void CaptureListenerOnCaptureResultAvailable(object sender, CaptureResult result)
        {
            switch (State)
            {
                case CaptureState.WaitingLock:
                {
                    var afState = (Integer) result.Get(CaptureResult.ControlAfState);
                    if (afState == null)
                    {
                        CaptureStillPicture();
                    }

                    else if ((int) ControlAFState.FocusedLocked == afState.IntValue() ||
                             (int) ControlAFState.NotFocusedLocked == afState.IntValue())
                    {
                        // ControlAeState can be null on some devices
                        var aeState = (Integer) result.Get(CaptureResult.ControlAeState);
                        if (aeState == null ||
                            aeState.IntValue() == (int) ControlAEState.Converged)
                        {
                            State = CaptureState.PictureTaken;
                            CaptureStillPicture();
                        }
                        else
                        {
                            RunPrecaptureSequence();
                        }
                    }

                    break;
                }
                case CaptureState.WaitingPrecapture:
                {
                    // ControlAeState can be null on some devices
                    var aeState = (Integer) result.Get(CaptureResult.ControlAeState);
                    if (aeState == null ||
                        aeState.IntValue() == (int) ControlAEState.Precapture ||
                        aeState.IntValue() == (int) ControlAEState.FlashRequired)
                        State = CaptureState.WaitingNonPrecapture;

                    break;
                }
                case CaptureState.WaitingNonPrecapture:
                {
                    // ControlAeState can be null on some devices
                    var aeState = (Integer) result.Get(CaptureResult.ControlAeState);
                    if (aeState == null || aeState.IntValue() != (int) ControlAEState.Precapture)
                    {
                        State = CaptureState.PictureTaken;
                        CaptureStillPicture();
                    }

                    break;
                }

                case CaptureState.Preview:
                    break;
                case CaptureState.PictureTaken:
                    break;
                default:
                    throw new NotSupportedException("This camera state is not supported.");
            }
        }

        // Run the precapture sequence for capturing a still image. This method should be called when
        // we get a response in {@link #mCaptureCallback} from {@link #lockFocus()}.
        private void RunPrecaptureSequence()
        {
            try
            {
                // This is how to tell the camera to trigger.
                _previewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger,
                    (int) ControlAEPrecaptureTrigger.Start);
                // Tell #mCaptureCallback to wait for the precapture sequence to be set.
                State = CaptureState.WaitingPrecapture;
                _captureSession.Capture(_previewRequestBuilder.Build(), _captureListener, _backgroundThread.Handler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        private void CaptureStillPicture()
        {
            try
            {
                // This is the CaptureRequest.Builder that we use to take a picture.
                if (_stillCaptureBuilder == null)
                    _stillCaptureBuilder = _cameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);

                _stillCaptureBuilder.AddTarget(_imageReader.Surface);

                // Use the same AE and AF modes as the preview.
                _stillCaptureBuilder.Set(CaptureRequest.ControlAfMode, (int) ControlAFMode.ContinuousPicture);
                Utils.SetAutoFlash(_stillCaptureBuilder, _manager, _cameraId);

                var rotation = Utils.CalculateRotation();
                _stillCaptureBuilder.Set(CaptureRequest.JpegOrientation, Utils.GetOrientation(rotation));

                _captureSession.StopRepeating();

                var stillCaptureListener = new CameraCaptureListener();
                stillCaptureListener.CaptureResultAvailable += StillCaptureHandler;
                _captureSession.Capture(_stillCaptureBuilder.Build(),
                    stillCaptureListener, null);
            }
            catch (CameraAccessException e)
            {
                Debug.WriteLine(e);
                throw;
            }
        }

        private void StillCaptureHandler(object sender, CaptureResult e)
        {
            ((CameraCaptureListener) sender).CaptureResultAvailable -= StillCaptureHandler;
            UnlockFocus();
        }

        // Lock the focus as the first step for a still image capture.
        private void LockFocus()
        {
            try
            {
                // This is how to tell the camera to lock focus.
                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int) ControlAFTrigger.Start);
                // Tell #mCaptureCallback to wait for the lock.
                State = CaptureState.WaitingLock;
                _captureSession.Capture(_previewRequestBuilder.Build(), _captureListener,
                    _backgroundThread.Handler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        // Unlock the focus. This method should be called when still image capture sequence is
        // finished.
        private void UnlockFocus()
        {
            try
            {
                // Reset the auto-focus trigger
                _previewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int) ControlAFTrigger.Cancel);
                Utils.SetAutoFlash(_previewRequestBuilder, _manager, _cameraId);
                _captureSession.Capture(_previewRequestBuilder.Build(), _captureListener,
                    _backgroundThread.Handler);
                // After this, the camera will go back to the normal state of preview.
                State = CaptureState.WaitingLock;
                _captureSession.SetRepeatingRequest(_previewRequest, _captureListener,
                    _backgroundThread.Handler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
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
            if (_hasPreview)
                _cameraPreview =
                    new CameraPreview(_context, _cameraDevice, _manager, _backgroundThread.Handler, _captureListener);

            _asyncAutoResetEvent.Set();
            _asyncAutoResetEvent = null;
        }
    }
}