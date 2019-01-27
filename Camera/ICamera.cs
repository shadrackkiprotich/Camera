using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Camera
{
    /// <summary>
    ///     Can be a logical or a physical camera.
    /// </summary>
    // ReSharper disable once InheritdocConsiderUsage
    public interface ICamera : IDisposable
    {
        /// <summary>
        ///     Opens the camera.
        /// </summary>
        Task OpenAsync();

        Task<ICameraPreview> OpenWithPreviewAsync(Size previewRequestSize);

        Task<byte[]> TakePictureAsync();

        /// <summary>
        ///     Closes the camera.
        /// </summary>
        void Close();
    }
}