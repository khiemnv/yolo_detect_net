using annotation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace comp
{
    public static class BoxExtensions
    {
        public static annotation.YoloBox ToYoloBox(this IRect rec, double w, double h)
        {
            var (x, y, dx, dy) = ((double)rec.X, (double)rec.Y, (double)rec.Width, (double)rec.Height);
            return new YoloBox { cx = (x + (dx / 2)) / w, cy = (y + (dy / 2)) / h, width = dx / w, height = dy / h };
        }
        public static YoloBox ToYoloBox(this Rectangle rec, double w, double h)
        {
            var (x, y, dx, dy) = ((double)rec.X, (double)rec.Y, (double)rec.Width, (double)rec.Height);
            return new YoloBox { cx = (x + (dx / 2)) / w, cy = (y + (dy / 2)) / h, width = dx / w, height = dy / h };
        }

        public static Rectangle FromYoloBox(this YoloBox box, double w, double h)
        {
            var (cx, cy, dx, dy) = (box.cx * w, box.cy * h, box.width * w, box.height * h);
            return new Rectangle((int)Math.Round(cx - dx / 2),
                (int)Math.Round(cy - dy / 2),
                (int)Math.Round(dx), (int)Math.Round(dy));
        }

        public static Rectangle CrtRec(Point a, Point b)
        {
            var (xmin, dx) = minAndDiff(a.X, b.X);
            var (ymin, dy) = minAndDiff(a.Y, b.Y);
            return new Rectangle(xmin, ymin, dx, dy);
        }
        public static Rectangle CrtRec(Point a, Point b, int margin, int W, int H)
        {
            var (xmin, dx) = minAndDiff(a.X, b.X);
            if (xmin < margin) { xmin = 0; dx += xmin; }
            else { xmin -= margin; dx += margin; }

            var (ymin, dy) = minAndDiff(a.Y, b.Y);
            if (ymin < margin) { ymin = 0; dy += ymin; }
            else { ymin -= margin; dy += margin; }

            if (ymin + dy < H - margin)
            {
                dy += margin;
            }
            else { dy = H - ymin; }
            if (xmin + dx < W - margin)
            {
                dx += margin;
            }
            else { dx = W - xmin; }

            return new Rectangle(xmin, ymin, dx, dy);
        }

        private static (int xmin, int dx) minAndDiff(int x1, int x2)
        {
            return x1 >= x2 ? (x2, x1 - x2) : (x1, x2 - x1);
        }

        public static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        public static Size Scale(this Size org, float scale)
        {
            return new Size((int)Math.Round(org.Width / scale), (int)Math.Round(org.Height / scale));
        }

        public static float Overlap(
            this IBox a, IBox b)
        {
            var (rect1, rect2) = (new RectangleF(a.X, a.Y, a.W, a.H), new RectangleF(b.X, b.Y, b.W, b.H));

            var intersection = RectangleF.Intersect(rect1, rect2);

            var intArea = intersection.Area(); // intersection area
            var unionArea = rect1.Area() + rect2.Area() - intArea; // union area
            var overlap = intArea / unionArea; // overlap ratio
            return overlap;
        }

        public static Rectangle FromPoints(IEnumerable<PointF> points)
        {
            var xmin = (int)points.Min(p => p.X);
            var xmax = (int)points.Max(p => p.X);
            var ymin = (int)points.Min(p => p.Y);
            var ymax = (int)points.Max(p => p.Y);
            var rect = new Rectangle(xmin, ymin, xmax - xmin, ymax - ymin);
            return rect;
        }
    }
}
