
using System.Collections.Generic;

namespace annotation
{
    public interface IBox
    {
        string LabelName { get; }
        float X { get; }
        float Y { get; }
        float W { get; }
        float H { get; }

        IEnumerable<System.Drawing.PointF> Points { get; }
    }
}
public interface IRect
{
    float X { get; set; }
    float Y { get; set; }
    float Width { get; set; }
    float Height { get; set; }
    float Right { get; set; }
    float Bottom { get; set; }
}

public interface IPoint
{
    float X { get; set; }
    float Y { get; set; }
}