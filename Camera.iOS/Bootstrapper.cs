using Camera.Internals;
using Xamarin.Forms;

namespace Camera.iOS
{
    public static class Bootstrapper
    {
        public static void Init()
        {
            DependencyService.Register<ICameraManager, CameraManager>();
        }
    }
}