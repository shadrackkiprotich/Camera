using System;
using System.IO;

namespace Camera
{
    /// <summary>
    ///     Can be a logical or a physical camera.
    /// </summary>
    // ReSharper disable once InheritdocConsiderUsage
    public interface ICamera
    {
        /// <summary>
        ///     Triggers when a frame is available for preview.
        /// </summary>
        event EventHandler<Stream> PreviewFrameAvailable;

        void Open();

        void Close();
    }
}