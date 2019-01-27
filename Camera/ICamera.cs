using System.Threading.Tasks;

namespace Camera
{
    /// <summary>
    ///     Can be a logical or a physical camera.
    /// </summary>
    // ReSharper disable once InheritdocConsiderUsage
    public interface ICamera
    {
        IPreview Preview { get; }

        /// <summary>
        ///     Opens the camera.
        /// </summary>
        Task OpenAsync();

        byte[] TakePicture();

        /// <summary>
        ///     Closes the camera.
        /// </summary>
        void Close();
    }
}