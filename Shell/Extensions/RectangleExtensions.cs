using System.Drawing;

namespace annotation
{
    public static class RectangleExtensions
    {
        /// <summary>
        /// Calculates rectangle area.
        /// </summary>
        public static float Area(this IRect source)
        {
            return source.Width * source.Height;
        }
        public static float Area(this RectangleF source)
        {
            return source.Width * source.Height;
        }
        public static int Area(this Rectangle source)
        {
            return source.Width * source.Height;
        }
        public static int Area(this YoloBox source)
        {
            return (int)(source.width * source.height);
        }
        public static int Area(this MyBox source)
        {
            return Area(source.Rect);
        }
    }
}
