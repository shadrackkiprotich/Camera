using System;
using Xamarin.Forms;

namespace Camera
{
    /// <summary>
    ///     The camera preview.
    /// </summary>
    public interface IPreview
    {
        Size Size { get; }

        System.Drawing.Size PixelSize { get; }

        /// <summary>
        ///     Triggers when a frame is available for preview.
        /// </summary>
        event EventHandler<byte[]> FrameAvailable;

        /// <summary>
        ///     Starts the camera preview.
        /// </summary>
        void Start(Size requestSize);

        /// <summary>
        ///     Stops the camera preview.
        /// </summary>
        void Stop();
    }
}