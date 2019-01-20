namespace Camera
{
    /// <summary>
    ///     The logical camera in a system. Each logical camera may or may not contain multiple physical cameras.
    ///     (e.g. In dual-camera phones)
    /// </summary>
    public enum LogicalCameras
    {
        /// <summary>
        ///     The logical front camera.
        /// </summary>
        Front,

        /// <summary>
        ///     The logical rear camera.
        /// </summary>
        Rear
    }
}