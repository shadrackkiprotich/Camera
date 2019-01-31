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
        private ICamera _camera;
        private long _framesRendered;
        private bool _isScaleMeasured;
        private byte[] _latestFrame;
        private Size _previewPixelSize;
        private float _scale;
        private long _totalMillis;

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
                _camera = CameraManager.Current.GetCamera(LogicalCameras.Front);
                var preview =
                    await _camera.OpenWithPreviewAsync(new Xamarin.Forms.Size(CanvasView.Width, CanvasView.Height));
                preview.FrameAvailable += PreviewOnFrameAvailable;
                _previewPixelSize = preview.PixelSize;
                Device.BeginInvokeOnMainThread(() =>
                    Size.Text = _previewPixelSize.Width + "x" + _previewPixelSize.Height);
                _stopwatch.Start();
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

                        if (canvasWidth > previewWidth) _scale = (float) canvasWidth / previewWidth;
                        if (canvasHeight > previewHeight)
                        {
                            var scaleTemp = (float) canvasHeight / previewHeight;
                            if (_scale > scaleTemp) _scale = scaleTemp;
                        }

                        _isScaleMeasured = true;
                    }

                    var canvas = e.Surface.Canvas;

                    canvas.Scale(_scale);
                    canvas.Scale(-1.0f, 1.0f, (float) previewHeight / 2, 0);
                    canvas.RotateDegrees(-90, 0, 0);
                    canvas.Translate(-previewWidth, 0);
                    canvas.DrawColor(SKColors.Black);

                    try
                    {
                        canvas.DrawImage(image, 0, 0);
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        throw;
                    }
                }
            }

            if (!hasNewFrame) return;
            _totalMillis += _stopwatch.ElapsedMilliseconds;
            _framesRendered++;

            if (_totalMillis < 1000) return;

            _stopwatch.Restart();
            var fps = _framesRendered / (_totalMillis / 1000);
            Device.BeginInvokeOnMainThread(() => Fps.Text = fps.ToString());

            if (_totalMillis < 5000) return;
            _framesRendered = fps;
            _totalMillis = 1000;
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

        private async void Button_OnClicked(object sender, EventArgs e)
        {
            Debug.WriteLine(await _camera.TakePictureAsync());
        }
    }
}