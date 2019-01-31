using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AVFoundation;
using CoreMedia;
using CoreVideo;
using Xamarin.Forms;

namespace Camera.iOS
{
    public class CameraPreview : AVCaptureVideoDataOutputSampleBufferDelegate, ICameraPreview
    {
        private Stopwatch _stopwatch;

        public CameraPreview(Size size, System.Drawing.Size pixelSize)
        {
            Size = size;
            PixelSize = pixelSize;
        }

        public long MillisecondsSinceLastFrame => _stopwatch.ElapsedMilliseconds;

//        private CGImage ImageFromSampleBuffer(CMSampleBuffer sampleBuffer)
//        {
//            var imageBuffer = sampleBuffer.GetImageBuffer();
//            var ciImage = new CIImage(imageBuffer);
//            return _context.CreateCGImage(ciImage, ciImage.Extent);
//        }

        public Size Size { get; }
        public System.Drawing.Size PixelSize { get; }

        public Transform Transform { get; } = new Transform();

        public event EventHandler<byte[]> FrameAvailable;

        public override void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer,
            AVCaptureConnection connection)
        {
            if (_stopwatch == null)
            {
                _stopwatch = Stopwatch.StartNew();
            }
            else if (_stopwatch.ElapsedMilliseconds < 33)
            {
                sampleBuffer.Dispose();
                return;
            }
            else
            {
                _stopwatch.Restart();
            }

            if (!(sampleBuffer?.GetImageBuffer() is CVPixelBuffer pixelBuffer))
            {
                sampleBuffer?.Dispose();
                return;
            }

            pixelBuffer.Lock(CVPixelBufferLock.ReadOnly);
            var width = pixelBuffer.Width;
            var height = pixelBuffer.Height;
            var length = (int) (width * height * 4);
            var baseAddress = pixelBuffer.BaseAddress;
            var buffer = new byte[length];
            Marshal.Copy(baseAddress, buffer, 0, length);

            Task.Run(() =>
            {
                var frame = ToRgba8888(buffer, length);
                FrameAvailable?.Invoke(this, frame);
            });

            pixelBuffer.Unlock(CVPixelBufferLock.ReadOnly);
            pixelBuffer.Dispose();
            sampleBuffer.Dispose();
        }

        public override void DidDropSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer,
            AVCaptureConnection connection)
        {
//            sampleBuffer.Dispose();
        }

        private byte[] ToRgba8888(byte[] bgra, int length)
        {
            var rgba = new byte[length];
            unsafe
            {
                fixed (byte* bgraFixed = bgra)
                fixed (byte* rgbaFixed = rgba)
                {
                    var rgbaPtr = rgbaFixed;
                    var bgraPtr = bgraFixed;
                    byte blue = 0;
                    byte green = 0;
                    byte red = 0;

                    for (var i = 0; i < length; i++)
                    {
                        if (i % 4 == 0)
                        {
                            blue = *bgraPtr;
                        }
                        else if ((i - 1) % 4 == 0)
                        {
                            green = *bgraPtr;
                        }
                        else if ((i - 2) % 4 == 0)
                        {
                            red = *bgraPtr;
                        }
                        else
                        {
                            *rgbaPtr = red;
                            rgbaPtr++;
                            *rgbaPtr = green;
                            rgbaPtr++;
                            *rgbaPtr = blue;
                            rgbaPtr++;

                            *rgbaPtr = *bgraPtr;
                            rgbaPtr++;
                        }

                        bgraPtr++;
                    }
                }
            }

            return rgba;
        }
    }
}