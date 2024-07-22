using OpenCvSharp;

namespace Yolov
{
    public class VinoMask : IMask
    {
        public Mat mask;

        public VinoMask(int w, int h)
        {
            mask = new Mat(h, w, MatType.CV_8UC1);
        }

        public float this[int x, int y] => YoloModelBase.GetConfidence(mask.At<Vec3b>(y, x)[0]);

        //float IMask.this[int x, int y] { get => mask.At<float>(x, y); set => mask.At<float>(x, y)=value; }

        public int Width => mask.Width;

        public int Height => mask.Height;

        public float GetConfidence(int x, int y)
        {
            return YoloModelBase.GetConfidence(mask.At<Vec3b>(y, x)[0]);
        }
    }
}
