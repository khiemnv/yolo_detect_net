using Point = System.Drawing.Point;


public class RectangleCustom
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public Point C { get; }
    public string Name { get; set; }
    public int Area { get; set; }
    public int Percent { get; set; }
    public RectangleCustom(System.Drawing.Point center, int w, int h, string name)
    {
        this.X = center.X - w / 2;
        this.Y = center.Y - h / 2;
        this.W = w;
        this.H = h;
        this.C = center;
        this.Area = w * h;
        this.Name = name;
        this.Percent = 0;
    }
    public RectangleCustom(int x, int y, int w, int h, string name, int percent)
    {
        this.X = x;
        this.Y = y;
        this.W = w;
        this.H = h;
        C = Center();
        this.Area = w * h;
        this.Name = name;
        this.Percent = percent;
    }
    public RectangleCustom(int x, int y, int w, int h, string name)
    {
        this.X = x;
        this.Y = y;
        this.W = w;
        this.H = h;
        C = Center();
        this.Area = w * h;
        this.Name = name;
        this.Percent = 0;
    }
    public RectangleCustom(System.Drawing.Point ct, string Name)
    {
        this.C = new Point(ct.X, ct.Y);
        this.Name = Name;
        this.Percent = 0;
    }
    public RectangleCustom() { }
    public System.Drawing.Point Center()
    {
        System.Drawing.Point centerPoint = new System.Drawing.Point
        {
            X = this.X + this.W / 2,
            Y = this.Y + this.H / 2
        };
        return centerPoint;
    }
    public Rectangle RectangleDraw()
    {
        Rectangle r = new Rectangle
        {
            X = this.X,
            Y = this.Y,
            Width = this.W,
            Height = this.H
        };
        return r;
    }
}


