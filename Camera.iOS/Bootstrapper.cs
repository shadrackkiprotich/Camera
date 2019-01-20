using Camera.Internals;
using Xamarin.Forms;

namespace Camera.iOS
{
    public static class Bootstrapper
    {
        public static void Init()
        {
            DependencyService.Register<ICamera, Camera>();
            DependencyService.Register<ICameraManager, CameraManager>();
        }
    }
}