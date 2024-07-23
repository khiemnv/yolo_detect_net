public class PredictionBox : IBox
{
    public static IRect Intersect(IRect a, IRect b)
    {
        float x1 = System.Math.Max(a.X, b.X);
        float x2 = System.Math.Min(a.Right, b.Right);
        float y1 = System.Math.Max(a.Y, b.Y);
        float y2 = System.Math.Min(a.Bottom, b.Bottom);

        if (x2 >= x1 && y2 >= y1)
        {
            return RectangleF(x1, y1, x2 - x1, y2 - y1);
        }

        return Empty;
    }
    public static IRect Empty = new MyRect();
    public static IRect RectangleF(float x, float y, float width, float height)
    {
        return new MyRect(x, y, width, height);
    }
    public class Label
    {
        public int id = -1;
        public string name = "";
    }

    private readonly Label label = new Label();
    //public Label label { get { return _label; } }
    public class MyRect : IRect
    {
        public MyRect() { }
        public MyRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Right { get => X + Width; set => Width = X - value; }
        public float Bottom { get => Y + Height; set => Height = Y - value; }
    }
    public IRect rectangle = new MyRect();
    public float score;
    public int index;
    public List<IPoint> poses;

    public float X { get => rectangle.X; set => rectangle.X = value; } // 10,
    public float Y { get => rectangle.Y; set => rectangle.Y = value; } // 10,
    public float W { get => rectangle.Width; set => rectangle.Width = value; } // 20,
    public float H { get => rectangle.Height; set => rectangle.Height = value; } // 30

    public string LabelName { get => label.name; set => label.name = value; }
    public PredictionBox() { }
    public PredictionBox(int id, string name, float x, float y, float w, float h)
    {
        label.id = id; label.name = name;
        rectangle.X = x; rectangle.Y = y;
        rectangle.Width = w; rectangle.Height = h;
    }
}