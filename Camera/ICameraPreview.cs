using System;
using Xamarin.Forms;

namespace Camera
{
    /// <summary>
    ///     The camera preview.
    /// </summary>
    // ReSharper disable once InheritdocConsiderUsage
    public interface ICameraPreview : IDisposable
    {
        Size Size { get; }

        System.Drawing.Size PixelSize { get; }

        /// <summary>
        ///     Triggers when a frame is available for preview.
        /// </summary>
        event EventHandler<byte[]> FrameAvailable;
    }
}