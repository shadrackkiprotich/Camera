namespace Camera.Internals
{
    /// <summary>
    ///     For internal use by the Camera API.
    /// </summary>
    public interface ICameraManager
    {
        ICamera GetCamera(LogicalCameras camera);
    }
}