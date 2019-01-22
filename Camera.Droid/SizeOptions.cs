using System.Collections.Generic;
using Android.Util;

namespace Camera.Droid
{
    public class SizeOptions
    {
        public SizeOptions(List<Size> bigEnough, List<Size> notBigEnough)
        {
            BigEnough = bigEnough;
            NotBigEnough = notBigEnough;
        }

        public List<Size> BigEnough { get; }

        public List<Size> NotBigEnough { get; }
    }
}