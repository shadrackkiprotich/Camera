namespace Camera
{
    public class Transform
    {
        public float TranslateX { get; set; }
        public float TranslateY { get; set; }
        public float ScaleX { get; set; } = 1;
        public float ScaleY { get; set; } = 1;
        public float ScalePivotX { get; set; }
        public float ScalePivotY { get; set; }
        public float RotateDegrees { get; set; }
        public float RotatePivotX { get; set; }
        public float RotatePivotY { get; set; }
    }
}