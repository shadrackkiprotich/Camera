using Android.App;
using Android.Renderscripts;

namespace Camera.Droid
{
    public static class Bootstrapper
    {
        internal static RenderScript Rs { get; private set; }

        public static void Init(RenderScript rs = null)
        {
            if (rs == null) rs = RenderScript.Create(Application.Context);

            Rs = rs;
        }
    }
}