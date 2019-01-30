using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;
using AVFoundation;
using CoreFoundation;
using CoreGraphics;
using CoreVideo;
using Foundation;
using UIKit;
using Xamarin.Forms;

namespace Camera.iOS
{
    public class Camera : ICamera
    {
        private readonly AVCaptureDevice _camera;
        private readonly Timer _previewStopChecker;
        private readonly AVCaptureSession _session;
        private readonly DispatchQueue _sessionQueue;
        private CameraPreview _cameraPreview;
        private bool _isRunning;
        private AVCapturePhotoOutput _photoOutput;

        public Camera(AVCaptureDevice camera)
        {
            _camera = camera;
            _session = new AVCaptureSession();
            _sessionQueue = new DispatchQueue(_camera.UniqueID);
            _previewStopChecker = new Timer(100);
            ConfigureCameraForDevice(camera);
            InitializeSessionInput();
        }

        public async Task OpenAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<ICameraPreview> OpenWithPreviewAsync(Size previewRequestSize)
        {
            var are = new AsyncAutoResetEvent(false);
            _sessionQueue.DispatchAsync(() =>
            {
                _cameraPreview = DispatchOpenWithPreviewAsync(previewRequestSize);
                are.Set();
            });
            await are.WaitAsync(TimeSpan.FromSeconds(5));
            _previewStopChecker.Elapsed += PreviewStopCheckerOnElapsed;
            return _cameraPreview;
        }

        public async Task<byte[]> TakePictureAsync()
        {
            var asyncAre = new AsyncAutoResetEvent(false);
            NSData data = null;
            _photoOutput.CapturePhoto(CreatePhotoSettings(), new CapturePhotoDelegate(photo =>
            {
                var image = new UIImage(photo.FileDataRepresentation);
                image = FixOrientation(image);
                data = image.AsJPEG((nfloat) 1.0);
                asyncAre.Set();
            }));
            await asyncAre.WaitAsync(TimeSpan.FromSeconds(5));
            return data.ToArray();
        }

        public void Close()
        {
            _session.StopRunning();
            _isRunning = false;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private void PreviewStopCheckerOnElapsed(object sender, ElapsedEventArgs e)
        {
            var millisecondsSinceLastFrame = _cameraPreview?.MillisecondsSinceLastFrame;
            if (millisecondsSinceLastFrame != null && millisecondsSinceLastFrame.Value > 100)
                _sessionQueue.DispatchAsync(() =>
                {
                    _session.StopRunning();
                    _session.StartRunning();
                });
        }

        private CameraPreview DispatchOpenWithPreviewAsync(Size previewRequestSize)
        {
            _session.BeginConfiguration();
            var videoOutput = new AVCaptureVideoDataOutput();
            var settings = new AVVideoSettingsUncompressed {PixelFormatType = CVPixelFormatType.CV32BGRA};
            videoOutput.UncompressedVideoSetting = settings;
            videoOutput.WeakVideoSettings = settings.Dictionary;
            videoOutput.AlwaysDiscardsLateVideoFrames = true;

            var preview = new CameraPreview(previewRequestSize, new System.Drawing.Size(720, 1280));
            videoOutput.SetSampleBufferDelegateQueue(preview, new DispatchQueue("sample buffer"));

            _session.AddOutput(videoOutput);

            var videoConnection = videoOutput.ConnectionFromMediaType(AVMediaType.Video);
            videoConnection.VideoOrientation = AVCaptureVideoOrientation.Portrait;
            videoConnection.VideoMirrored = true;

            _photoOutput = new AVCapturePhotoOutput
            {
                IsHighResolutionCaptureEnabled = true
            };
            _photoOutput.SetPreparedPhotoSettingsAsync(new[] {CreatePhotoSettings()});

            _session.SessionPreset = AVCaptureSession.Preset1280x720;
            _session.AddOutput(_photoOutput);
            _session.CommitConfiguration();
            _session.StartRunning();
            _isRunning = true;
            return preview;
        }

        private System.Drawing.Size ToPixels(Size previewRequestSize)
        {
            nfloat scale = 0;

            DispatchQueue.MainQueue.DispatchSync(() => { scale = UIScreen.MainScreen.Scale; });

            var (width, height) = previewRequestSize;
            var widthPx = width * scale;
            var heightPx = height * scale;
            return new System.Drawing.Size((int) widthPx, (int) heightPx);
        }

        private static AVCapturePhotoSettings CreatePhotoSettings()
        {
            var photoSettings =
                AVCapturePhotoSettings.FromFormat(
                    new NSDictionary<NSString, NSObject>(AVVideo.CodecKey, AVVideoCodecType.Jpeg.GetConstant()));
            photoSettings.IsHighResolutionPhotoEnabled = true;
            photoSettings.IsAutoStillImageStabilizationEnabled = true;
            return photoSettings;
        }

        public void ConfigureCameraForDevice(AVCaptureDevice device)
        {
            var error = new NSError();
            if (device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus))
            {
                device.LockForConfiguration(out error);
                device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
                device.UnlockForConfiguration();
            }
            else if (device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
            {
                device.LockForConfiguration(out error);
                device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
                device.UnlockForConfiguration();
            }
            else if (device.IsWhiteBalanceModeSupported(AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance))
            {
                device.LockForConfiguration(out error);
                device.WhiteBalanceMode = AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance;
                device.UnlockForConfiguration();
            }
        }

        private UIImage FixOrientation(UIImage image)
        {
//            image.Scale(newSize, image.CurrentScale);
            var bounds = new CGRect(CGPoint.Empty, image.Size);
            var newSize = CGAffineTransform.CGRectApplyAffineTransform(bounds,
                CGAffineTransform.MakeRotation(-(nfloat) Math.PI / 2)).Size;

            UIGraphics.BeginImageContextWithOptions(newSize, true, image.CurrentScale);
            var context = UIGraphics.GetCurrentContext();

            context.TranslateCTM(newSize.Width / 2, newSize.Height / 2);
            image.Draw(new CGRect(-image.Size.Width / 2, -image.Size.Height / 2, image.Size.Width, image.Size.Height));

            var newImage = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();

            return newImage;
        }

        private void InitializeSessionInput()
        {
            var input = new AVCaptureDeviceInput(_camera, out var error);
            Debug.WriteLine(error);
            _session.AddInput(input);
        }

        private class CapturePhotoDelegate : AVCapturePhotoCaptureDelegate
        {
            private readonly Action<AVCapturePhoto> handler;

            public CapturePhotoDelegate(Action<AVCapturePhoto> handler)
            {
                this.handler = handler;
            }

            public override void DidFinishProcessingPhoto(AVCapturePhotoOutput output, AVCapturePhoto photo,
                NSError error)
            {
                Debug.WriteLine(error);
                handler(photo);
            }
        }
    }
}