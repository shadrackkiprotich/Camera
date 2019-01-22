using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using Camera;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using SkiaSharp;
using SkiaSharp.Views.Forms;

namespace Sample
{
    public partial class MainPage
    {
        private readonly Queue<byte[]> _pendingFrames = new Queue<byte[]>();
        private byte[] _latestFrame;
        private Size _previewPixelSize;

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
            if (_pendingFrames.Count > 0) _latestFrame = _pendingFrames.Dequeue();

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

                    var widthDiff = Math.Abs(previewWidth - canvasWidth);
                    var heightDiff = Math.Abs(previewHeight - canvasHeight);

                    e.Surface.Canvas.Translate((float) widthDiff / 2, (float) heightDiff / 2);
                    e.Surface.Canvas.Scale(new SKPoint(previewWidth, -previewHeight));
                    e.Surface.Canvas.RotateDegrees(-90, (float) previewWidth / 2, (float) previewHeight / 2);
                    e.Surface.Canvas.DrawImage(image, 0, 0);
                }
            }
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