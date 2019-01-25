using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Camera;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Size = System.Drawing.Size;

namespace Sample
{
    public partial class MainPage
    {
        private readonly Queue<byte[]> _pendingFrames = new Queue<byte[]>();
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private int _framesRendered;
        private bool _isScaleMeasured;
        private byte[] _latestFrame;
        private Size _previewPixelSize;
        private float _scale;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RequestPermission(Permission.Camera);
            try
            {
                CanvasView.PaintSurface += CanvasViewOnPaintSurface;
                var camera = CameraManager.Current.GetCamera(LogicalCameras.Front);
                await camera.OpenAsync();
                camera.Preview.FrameAvailable += PreviewOnFrameAvailable;
                camera.Preview.Start(new Xamarin.Forms.Size(CanvasView.Width, CanvasView.Height));
                _previewPixelSize = camera.Preview.PixelSize;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            const string message = "Camera opened";
            Debug.WriteLine(message);
        }

        private void PreviewOnFrameAvailable(object sender, byte[] buffer)
        {
            _pendingFrames.Enqueue(buffer);
        }

        private unsafe void CanvasViewOnPaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            var hasNewFrame = false;
            if (_pendingFrames.Count > 0)
            {
                _latestFrame = _pendingFrames.Dequeue();
                hasNewFrame = true;
            }

            if (null == _latestFrame) return;

            var imageInfo = new SKImageInfo(_previewPixelSize.Width,
                _previewPixelSize.Height,
                SKColorType.Rgba8888);

            fixed (byte* numPtr = _latestFrame)
            {
                using (var image = SKImage.FromPixels(imageInfo, (IntPtr) numPtr))
                {
                    var canvasWidth = e.BackendRenderTarget.Width;
                    var canvasHeight = e.BackendRenderTarget.Height;
                    var previewWidth = _previewPixelSize.Width;
                    var previewHeight = _previewPixelSize.Height;

                    if (!_isScaleMeasured)
                    {
                        _scale = 1.0f;

                        if (canvasWidth > previewHeight) _scale = (float) canvasWidth / previewHeight;
                        if (canvasHeight > previewWidth)
                        {
                            var scaleTemp = (float) canvasHeight / previewWidth;
                            if (_scale > scaleTemp) _scale = scaleTemp;
                        }

                        _isScaleMeasured = true;
                        _stopwatch.Start();
                    }

                    e.Surface.Canvas.Scale(_scale);
                    e.Surface.Canvas.Scale(-1.0f, 1.0f, (float) previewHeight / 2, 0);
                    e.Surface.Canvas.RotateDegrees(-90, 0, 0);
                    e.Surface.Canvas.Translate(-previewWidth, 0);
                    e.Surface.Canvas.DrawImage(image, 0, 0);
                }
            }

            if (!hasNewFrame) return;
            var seconds = _stopwatch.Elapsed.Seconds;
            if (seconds == 0) return;

            _framesRendered++;
            Device.BeginInvokeOnMainThread(() => Fps.Text = (_framesRendered / seconds).ToString());
        }

        private static async Task RequestPermission(Permission permission)
        {
            try
            {
                var status = await CrossPermissions.Current.CheckPermissionStatusAsync(permission);
                if (status != PermissionStatus.Granted)
                    await CrossPermissions.Current.RequestPermissionsAsync(permission);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }
}