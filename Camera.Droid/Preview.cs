using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Com.Image.Yuv420888;
using Java.Lang;
using Java.Util;
using Xamarin.Forms;
using Application = Android.App.Application;
using Boolean = Java.Lang.Boolean;
using Debug = System.Diagnostics.Debug;
using Image = Android.Media.Image;

namespace Camera.Droid
{
    public class Preview : IPreview
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
        private readonly Android.Hardware.Camera2.CameraManager _manager;
        private ImageAvailableListener _imageAvailableListener;
        private ImageReader _imageReader;
        private Yuv420888 _bufferFrame;

        private CaptureRequest.Builder _previewRequestBuilder;

        public Preview(CameraDevice camera, Android.Hardware.Camera2.CameraManager manager, Handler backgroundHandler)
        {
            _camera = camera;
            _manager = manager;
            _backgroundHandler = backgroundHandler;
        }

        public Size Size { get; private set; }

        public System.Drawing.Size PixelSize { get; private set; }

        public event EventHandler<byte[]> FrameAvailable;

        public void Start(Size requestSize)
        {
            var bufferSize = GetBufferSize(ToPixels(requestSize));
            Size = DimensionUtils.ToXamarinFormsSize(bufferSize);
            var pixelSize = new System.Drawing.Size {Width = bufferSize.Width, Height = bufferSize.Height};
            PixelSize = pixelSize;

            _imageReader = ImageReader.NewInstance(bufferSize.Width, bufferSize.Height, ImageFormatType.Yuv420888, 4);
            _imageAvailableListener = new ImageAvailableListener();
            _imageAvailableListener.ImageAvailable += PreviewImageAvailable;
            _imageReader.SetOnImageAvailableListener(_imageAvailableListener, _backgroundHandler);

            _previewRequestBuilder = _camera.CreateCaptureRequest(CameraTemplate.Preview);
            _previewRequestBuilder.AddTarget(_imageReader.Surface);
            _previewRequestBuilder.Set(CaptureRequest.ControlAfMode, (int) ControlAFMode.ContinuousPicture);

            SetAutoFlash(_previewRequestBuilder);

            var stateCallback = new PreviewStateCallback(_previewRequestBuilder.Build(), _backgroundHandler);
            var surfaces = new List<Surface> {_imageReader.Surface};
            _camera.CreateCaptureSession(surfaces, stateCallback, null);
        }

        public void Stop()
        {
            _imageAvailableListener.ImageAvailable -= PreviewImageAvailable;
            _imageReader.Close();
        }

        private void SetAutoFlash(CaptureRequest.Builder requestBuilder)
        {
            var flashSupported = GetFlashSupported();

            if (flashSupported) requestBuilder.Set(CaptureRequest.ControlAeMode, (int) ControlAEMode.OnAutoFlash);
        }

        private bool GetFlashSupported()
        {
            var characteristics = _manager.GetCameraCharacteristics(_camera.Id);
            // Check if the flash is supported.
            var available = (Boolean) characteristics.Get(CameraCharacteristics.FlashInfoAvailable);
            if (available == null) return false;

            return (bool) available;
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
            {
                _bufferFrame = new Yuv420888(Bootstrapper.Rs, PixelSize.Width, PixelSize.Height, yRowStride,
                    uvPixelStride, uvRowStride);
            }

            var rgb = _bufferFrame.ToRgba8888(yValues, uValues, vValues);
            FrameAvailable?.Invoke(this, rgb);
        }

//        private byte[] ToRgb(IReadOnlyList<byte> yValues, IReadOnlyList<byte> uValues,
//            IReadOnlyList<byte> vValues, int uvPixelStride, int uvRowStride)
//        {
//            var width = PixelSize.Width;
//            var height = PixelSize.Height;
//            var rgb = new byte[width * height * 4];
//
//            var partitions = Partitioner.Create(0, height);
//            Parallel.ForEach(partitions, range =>
//            {
//                var (item1, item2) = range;
//                Parallel.For(item1, item2, y =>
//                {
//                    for (var x = 0; x < width; x++)
//                    {
//                        var yIndex = x + width * y;
//                        var currentPosition = yIndex * 4;
//                        var uvIndex = uvPixelStride * (x / 2) + uvRowStride * (y / 2);
//
//                        var yy = yValues[yIndex];
//                        var uu = uValues[uvIndex];
//                        var vv = vValues[uvIndex];
//
//                        var rTmp = yy + vv * 1436 / 1024 - 179;
//                        var gTmp = yy - uu * 46549 / 131072 + 44 - vv * 93604 / 131072 + 91;
//                        var bTmp = yy + uu * 1814 / 1024 - 227;
//
//                        rgb[currentPosition++] = (byte) (rTmp < 0 ? 0 : rTmp > 255 ? 255 : rTmp);
//                        rgb[currentPosition++] = (byte) (gTmp < 0 ? 0 : gTmp > 255 ? 255 : gTmp);
//                        rgb[currentPosition++] = (byte) (bTmp < 0 ? 0 : bTmp > 255 ? 255 : bTmp);
//                        rgb[currentPosition] = 255;
//                    }
//                });
//            });
//
//            return rgb;
//        }

        private unsafe byte[] ToRgb(byte[] yValuesArr, byte[] uValuesArr,
            byte[] vValuesArr, int uvPixelStride, int uvRowStride)
        {
            var width = PixelSize.Width;
            var height = PixelSize.Height;
            var rgb = new byte[width * height * 4];

            var partitions = Partitioner.Create(0, height);
            Parallel.ForEach(partitions, range =>
            {
                var (item1, item2) = range;
                Parallel.For(item1, item2, y =>
                {
                    for (var x = 0; x < width; x++)
                    {
                        var yIndex = x + width * y;
                        var currentPosition = yIndex * 4;
                        var uvIndex = uvPixelStride * (x / 2) + uvRowStride * (y / 2);

                        fixed (byte* rgbFixed = rgb)
                        fixed (byte* yValuesFixed = yValuesArr)
                        fixed (byte* uValuesFixed = uValuesArr)
                        fixed (byte* vValuesFixed = vValuesArr)
                        {
                            var rgbPtr = rgbFixed;
                            var yValues = yValuesFixed;
                            var uValues = uValuesFixed;
                            var vValues = vValuesFixed;

                            var yy = *(yValues + yIndex);
                            var uu = *(uValues + uvIndex);
                            var vv = *(vValues + uvIndex);

                            var rTmp = yy + vv * 1436 / 1024 - 179;
                            var gTmp = yy - uu * 46549 / 131072 + 44 - vv * 93604 / 131072 + 91;
                            var bTmp = yy + uu * 1814 / 1024 - 227;

                            rgbPtr = rgbPtr + currentPosition;
                            *rgbPtr = (byte) (rTmp < 0 ? 0 : rTmp > 255 ? 255 : rTmp);
                            rgbPtr++;

                            *rgbPtr = (byte) (gTmp < 0 ? 0 : gTmp > 255 ? 255 : gTmp);
                            rgbPtr++;

                            *rgbPtr = (byte) (bTmp < 0 ? 0 : bTmp > 255 ? 255 : bTmp);
                            rgbPtr++;

                            *rgbPtr = 255;
                        }
                    }
                });
            });

            return rgb;
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

        private static bool IsSwappedDimensions(CameraCharacteristics characteristics)
        {
            var displayRotation = CalculateRotation();
            //noinspection ConstantConditions
            var mSensorOrientation = (int) characteristics.Get(CameraCharacteristics.SensorOrientation);
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

        private static SurfaceOrientation CalculateRotation()
        {
            var service = Application.Context.GetSystemService(Context.WindowService);
            var display = service?.JavaCast<IWindowManager>()?.DefaultDisplay;
            return display?.Rotation ?? 0;
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