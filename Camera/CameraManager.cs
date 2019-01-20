using Camera.Internals;
using Xamarin.Forms;

namespace Camera
{
    /// <summary>
    ///     Manages the <see cref="Camera" /> instances.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once InheritdocConsiderUsage
    public sealed class CameraManager : ICameraManager
    {
        private static CameraManager _instance;

        private readonly ICameraManager _cameraManagerImplementation;

        private CameraManager()
        {
            _cameraManagerImplementation = DependencyService.Get<ICameraManager>();
        }

        /// <summary>
        ///     The current global instance of this class.
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public static CameraManager Current
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = new CameraManager();
                return _instance;
            }
        }

        public ICamera GetCamera(LogicalCameras camera)
        {
            return _cameraManagerImplementation.GetCamera(camera);
        }
    }
}