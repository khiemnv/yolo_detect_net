namespace WP_GUI.ModelAlgorithm
{
    public class ImgObject : Box
    {
        public Vector2D CenterPos { get => new Vector2D(X + W / 2, Y + H / 2); }
        public ImgObject() { }
        public ImgObject(Box box)
        {
            rectangle = box.rectangle;
            LabelName = box.LabelName;
            score = box.score;
        }
        public ImgObject(PredictionBox box)
        {
            rectangle = box.rectangle;
            LabelName = box.LabelName;
            score = box.score;
        }
        public ImgObject(string name, Double xmin, Double ymin, Double xmax, Double ymax, int percent)
        {
            this.LabelName = name;
            this.rectangle.X = (float)xmin;
            this.rectangle.Y = (float)ymin;
            this.rectangle.Width = (float)xmax - (float)xmin;
            this.rectangle.Height = (float)ymax - (float)ymin;
            this.score = (float)percent / 100;
        }
        public ImgObject(string name, Double xmin, Double ymin, Double xmax, Double ymax)
        {
            this.LabelName = name;
            this.X = (float)xmin;
            this.Y = (float)ymin;
            this.W = (float)xmax - (float)xmin;
            this.H = (float)ymax - (float)ymin;
        }

    }

    public struct ImageSize
    {
        public int width;
        public int height;
        public int depth;
    }
}
