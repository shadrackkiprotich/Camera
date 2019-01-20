using Android.OS;
using Java.Lang;
using Debug = System.Diagnostics.Debug;

namespace Camera.Droid
{
    public class CameraBackgroundThread
    {
        private HandlerThread _backgroundThread;
        public Handler Handler { get; private set; }

        // Starts a background thread and its {@link Handler}.
        public void Start()
        {
            _backgroundThread = new HandlerThread("CameraBackground");
            _backgroundThread.Start();
            Handler = new Handler(_backgroundThread.Looper);
        }

        // Stops the background thread and its {@link Handler}.
        public void Stop()
        {
            _backgroundThread.QuitSafely();
            try
            {
                _backgroundThread.Join();
                _backgroundThread = null;
                Handler = null;
            }
            catch (InterruptedException e)
            {
                Debug.WriteLine(e);
            }
        }
    }
}