using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Views;
using Com.Image.Yuv420888;
using Java.Lang;
using Java.Util;
using Xamarin.Forms;
using Application = Android.App.Application;
using Debug = System.Diagnostics.Debug;
using Image = Android.Media.Image;
using Math = System.Math;

namespace Camera.Droid
{
    public class CameraPreview : ICameraPreview
    {
        /// <summary>
        ///     Max preview width that is guaranteed by Camera2 API
        /// </summary>
        private const int MaxPreviewWidth = 1920;

        /// <summary>
        ///     Max preview height that is guaranteed by Camera2 API
        /// </summary>
        private const int MaxPreviewHeight = 1080;

        private readonly Handler _backgroundHandler;
        private readonly CameraDevice _camera;
        private readonly CameraCaptureListener _captureListener;

        private readonly Context _context;
        private readonly Android.Hardware.Camera2.CameraManager _manager;
        private Yuv420888 _bufferFrame;
        private ImageAvailableListener _imageAvailableListener;
        private ImageReader _imageReader;
        private bool _isDisposed;
        private int _rotation;

        public CameraPreview(Context context, CameraDevice camera, Android.Hardware.Camera2.CameraManager manager,
            Handler backgroundHandler,
            CameraCaptureListener captureListener)
        {
            _context = context;
            _camera = camera;
            _manager = manager;
            _backgroundHandler = backgroundHandler;
            _captureListener = captureListener;
        }

        public Size Size { get; private set; }

        public System.Drawing.Size PixelSize { get; private set; }

        public Transform Transform { get; } = new Transform();

        public event EventHandler<byte[]> FrameAvailable;

        public void Dispose()
        {
            if (_isDisposed) return;

            Stop();

            _backgroundHandler?.Dispose();
            _captureListener?.Dispose();
            _imageAvailableListener?.Dispose();
            _imageReader?.Dispose();
            _bufferFrame?.Dispose();
            _isDisposed = true;
        }

        public Surface CreateSurface(Size requestSize)
        {
            var requestPixelSize = ToPixels(requestSize);
            var bufferSize = GetBufferSize(requestPixelSize);
            Size = DimensionUtils.ToXamarinFormsSize(bufferSize);
            var pixelSize = new System.Drawing.Size {Width = bufferSize.Width, Height = bufferSize.Height};
            PixelSize = pixelSize;
            ConfigureTransform(requestPixelSize.Width, requestPixelSize.Height);
            _imageReader = ImageReader.NewInstance(bufferSize.Width, bufferSize.Height, ImageFormatType.Yuv420888, 4);
            _imageAvailableListener = new ImageAvailableListener();
            _imageAvailableListener.ImageAvailable += PreviewImageAvailable;
            _imageReader.SetOnImageAvailableListener(_imageAvailableListener, _backgroundHandler);
            return _imageReader.Surface;
        }

        private void ConfigureTransform(int requestWidth, int requestHeight)
        {
            var viewRect = new RectF(0, 0, requestHeight, requestWidth);
            var bufferRect = new RectF(0, 0, PixelSize.Height, PixelSize.Width);
            var centerX = viewRect.CenterX();
            var centerY = viewRect.CenterY();
            if ((int) SurfaceOrientation.Rotation90 == _rotation || (int) SurfaceOrientation.Rotation270 == _rotation)
            {
                Transform.TranslateX = centerX - bufferRect.CenterX();
                Transform.TranslateY = centerY - bufferRect.CenterY();
                var scale = Math.Max((float) requestWidth / PixelSize.Height, (float) requestHeight / PixelSize.Width);
                Transform.ScaleX = scale;
                Transform.ScaleY = scale;
                Transform.ScalePivotX = centerX;
                Transform.ScalePivotY = centerY;
                Transform.RotateDegrees = 90 * (_rotation - 2);
                Transform.RotatePivotX = centerX;
                Transform.RotatePivotY = centerY;
            }
            else if ((int) SurfaceOrientation.Rotation180 == _rotation)
            {
                Transform.RotateDegrees = 180;
                Transform.RotatePivotX = centerX;
                Transform.RotatePivotY = centerY;
            }
        }

        private void Stop()
        {
            _imageAvailableListener.ImageAvailable -= PreviewImageAvailable;
            _imageReader.Close();
        }

        private void PreviewImageAvailable(object sender, Image e)
        {
            var planes = e.GetPlanes();

            var buffer = planes[0].Buffer;
            var yValues = new byte[buffer.Remaining()];
            buffer.Get(yValues);

            buffer = planes[1].Buffer;
            var uValues = new byte[buffer.Remaining()];
            buffer.Get(uValues);

            buffer = planes[2].Buffer;
            var vValues = new byte[buffer.Remaining()];
            buffer.Get(vValues);

            var uvPixelStride = planes[1].PixelStride;
            var uvRowStride = planes[1].RowStride;
            var yRowStride = planes[0].RowStride;
            e.Close();

            if (_bufferFrame == null)
                _bufferFrame = new Yuv420888(Bootstrapper.Rs, PixelSize.Width, PixelSize.Height, yRowStride,
                    uvPixelStride, uvRowStride);

            var rgb = _bufferFrame.ToRgba8888(yValues, uValues, vValues);

            FrameAvailable?.Invoke(this, rgb);
        }

        private static Android.Util.Size ToPixels(Size dpSize)
        {
            var (width, height) = dpSize;
            var widthPx = DimensionUtils.ConvertDpToPixel((float) width);
            var heightPx = DimensionUtils.ConvertDpToPixel((float) height);
            return new Android.Util.Size(widthPx, heightPx);
        }

        private Android.Util.Size GetBufferSize(Android.Util.Size requestSize)
        {
            var characteristics = _manager.GetCameraCharacteristics(_camera.Id);
            var map = (StreamConfigurationMap) characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            // ReSharper disable once CoVariantArrayConversion
            var largest = (Android.Util.Size) Collections.Max(
                Arrays.AsList(map.GetOutputSizes((int) ImageFormatType.Yuv420888)),
                new CompareSizesByArea());

            var swappedDimensions = IsSwappedDimensions(characteristics);

            var rotatedPreviewSize = GetRotatedPreviewSize(requestSize, swappedDimensions);
            var maxPreviewSize = GetMaxPreviewSize(swappedDimensions);

            return ChooseOptimalSize(map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture))),
                rotatedPreviewSize.Width, rotatedPreviewSize.Height, maxPreviewSize.Width,
                maxPreviewSize.Height, largest);
        }

        private static Android.Util.Size GetRotatedPreviewSize(Android.Util.Size requestSize, bool swappedDimensions)
        {
            var rotatedPreviewWidth = requestSize.Width;
            var rotatedPreviewHeight = requestSize.Height;

            if (!swappedDimensions) return new Android.Util.Size(rotatedPreviewWidth, rotatedPreviewHeight);
            rotatedPreviewWidth = requestSize.Height;
            rotatedPreviewHeight = requestSize.Width;

            return new Android.Util.Size(rotatedPreviewWidth, rotatedPreviewHeight);
        }

        private bool IsSwappedDimensions(CameraCharacteristics characteristics)
        {
            var displayRotation = Utils.CalculateRotation();
            //noinspection ConstantConditions
            var mSensorOrientation = _rotation = (int) characteristics.Get(CameraCharacteristics.SensorOrientation);
            var swappedDimensions = false;
            switch (displayRotation)
            {
                case SurfaceOrientation.Rotation0:
                case SurfaceOrientation.Rotation180:
                    if (mSensorOrientation == 90 || mSensorOrientation == 270) swappedDimensions = true;
                    break;
                case SurfaceOrientation.Rotation90:
                case SurfaceOrientation.Rotation270:
                    if (mSensorOrientation == 0 || mSensorOrientation == 180) swappedDimensions = true;
                    break;
                default:
                    Debug.WriteLine("Display rotation is invalid: " + displayRotation);
                    break;
            }

            return swappedDimensions;
        }

        private static Android.Util.Size GetMaxPreviewSize(bool swappedDimensions)
        {
            var metrics = Application.Context.Resources.DisplayMetrics;
            var maxPreviewWidth = metrics?.WidthPixels ?? 0;
            var maxPreviewHeight = metrics?.HeightPixels ?? 0;

            if (swappedDimensions)
            {
                maxPreviewWidth = metrics?.HeightPixels ?? 0;
                maxPreviewHeight = metrics?.WidthPixels ?? 0;
            }

            if (maxPreviewWidth > MaxPreviewWidth) maxPreviewWidth = MaxPreviewWidth;

            if (maxPreviewHeight > MaxPreviewHeight) maxPreviewHeight = MaxPreviewHeight;

            return new Android.Util.Size(maxPreviewWidth, maxPreviewHeight);
        }

        private static Android.Util.Size ChooseOptimalSize(IReadOnlyList<Android.Util.Size> choices,
            int textureViewWidth,
            int textureViewHeight, int maxWidth, int maxHeight, Android.Util.Size aspectRatio)
        {
            var sizeOptions = GetSizeOptions(choices, textureViewWidth, textureViewHeight, maxWidth, maxHeight,
                aspectRatio);

            // Pick the smallest of those big enough. If there is no one big enough, pick the
            // largest of those not big enough.
            if (sizeOptions.BigEnough.Count > 0)
                return (Android.Util.Size) Collections.Min(sizeOptions.BigEnough, new CompareSizesByArea());

            if (sizeOptions.NotBigEnough.Count > 0)
                return (Android.Util.Size) Collections.Max(sizeOptions.NotBigEnough, new CompareSizesByArea());

            Debug.WriteLine("Couldn't find any suitable preview size");
            return choices[0];
        }

        private static SizeOptions GetSizeOptions(IReadOnlyList<Android.Util.Size> choices, int textureViewWidth,
            int textureViewHeight, int maxWidth, int maxHeight, Android.Util.Size aspectRatio)
        {
            // Collect the supported resolutions that are at least as big as the preview Surface
            var bigEnough = new List<Android.Util.Size>();
            // Collect the supported resolutions that are smaller than the preview Surface
            var notBigEnough = new List<Android.Util.Size>();
            var w = aspectRatio.Width;
            var h = aspectRatio.Height;

            foreach (var option in choices)
            {
                if (option.Width > maxWidth || option.Height > maxHeight ||
                    option.Height != option.Width * h / w) continue;
                if (option.Width >= textureViewWidth &&
                    option.Height >= textureViewHeight)
                    bigEnough.Add(option);
                else
                    notBigEnough.Add(option);
            }

            return new SizeOptions(bigEnough, notBigEnough);
        }
    }
}