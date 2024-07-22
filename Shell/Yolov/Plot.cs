using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.Linq;

namespace Yolov
{
    public static class Plot
    {
        public static List<Point> GetContour(SegmentationBoundingBox seg, (int Width, int Height) originImage)
        {
            // draw mask
            var masksLayer = new Image<Rgba32>(originImage.Width, originImage.Height);
            var color = getCorlorPalete(seg.id);
            var mask = new SixLabors.ImageSharp.Image<Rgba32>(seg.bounds.Width, seg.bounds.Height);
            for (int x = 0; x < seg.mask.Width; x++)
            {
                for (int y = 0; y < seg.mask.Height; y++)
                {
                    var value = seg.mask[x, y];

                    if (value > 0.5)
                        mask[x, y] = color;
                }
            }

            //mask.SaveAsJpeg("mask2.jpeg");
            masksLayer.Mutate(x => x.DrawImage(mask, seg.bounds.Location, 1F));

            var contours = ImageContoursHelper.FindContours(masksLayer);
            var lst = new List<Point>();
            foreach (var c in contours)
            {
                foreach (var p in c)
                {
                    lst.Add(p);
                }
            }
            return lst;
        }

        public static void Export(string img_path,
            List<PredictionBox> boxes,
            string outPath)
        {
            var originImage = SixLabors.ImageSharp.Image.Load<Rgba32>(img_path);
            boxes.ForEach(box =>
            {
                var color = getCorlorPalete(box.index);
                float thickness = originImage.Height / 500;
                // draw box
                var label = $"{box.label.name} {box.score:N}";
                var FontSize = (float)originImage.Height / 100;
                var textOptions = new TextOptions(SystemFonts.Get("Arial").CreateFont(FontSize));
                var textPadding = thickness;
                originImage.Mutate(context =>
                {
                    DrawBoundingBox(context,
                                    new Rectangle((int)box.X, (int)box.Y, (int)box.W, (int)box.H),
                                    color,
                                    thickness,
                                    0F,
                                    label,
                                    textOptions,
                                    textPadding);
                    if (box.poses != null)
                    {
                        int i = 0;
                        foreach (var po in box.poses)
                        {
                            var pen = new SolidPen(getCorlorPalete(i++), thickness);
                            var ellipse = new EllipsePolygon(po.X, po.Y, 70, 70);
                            context.Draw(pen, ellipse);
                        }
                    }
                });
            });


            originImage.SaveAsJpeg(outPath);
        }
        public static void Export(string img_path,
            List<SegmentationBoundingBox> segs,
            string outPath)
        {
            var originImage = SixLabors.ImageSharp.Image.Load<Rgba32>(img_path);
            segs.ForEach(seg =>
            {
                // draw mask
                var masksLayer = new Image<Rgba32>(originImage.Width, originImage.Height);
                var contoursLayer = new Image<Rgba32>(originImage.Width, originImage.Height);
                var color = getCorlorPalete(seg.id);
                var mask = new SixLabors.ImageSharp.Image<Rgba32>(seg.bounds.Width, seg.bounds.Height);
                for (int x = 0; x < seg.mask.Width; x++)
                {
                    for (int y = 0; y < seg.mask.Height; y++)
                    {
                        var value = seg.mask[x, y];

                        if (value > 0.5)
                            mask[x, y] = color;
                    }
                }

                //mask.SaveAsJpeg("mask2.jpeg");
                masksLayer.Mutate(x => x.DrawImage(mask, seg.bounds.Location, 1F));
                //masksLayer.SaveAsJpeg("mask3.jpeg");
                //if (options.ContoursThickness > 0F)
                float thickness = originImage.Height / 500;
                {
                    var contours = CreateContours(mask, color, thickness);
                    contoursLayer.Mutate(x => x.DrawImage(contours, seg.bounds.Location, 1F));
                }
                originImage.Mutate(x => x.DrawImage(masksLayer, .4F));
                originImage.Mutate(x => x.DrawImage(contoursLayer, 1F));

                // draw box
                var label = $"{seg.name} {seg.confidence:N}";
                var FontSize = (float)originImage.Height / 100;
                var textOptions = new TextOptions(SystemFonts.Get("Arial").CreateFont(FontSize));
                var textPadding = thickness;
                originImage.Mutate(context =>
                {
                    DrawBoundingBox(context,
                                    seg.bounds,
                                    color,
                                    thickness,
                                    0F,
                                    label,
                                    textOptions,
                                    textPadding);
                });
            });


            originImage.SaveAsJpeg(outPath);
        }

        private static void DrawBoundingBox(IImageProcessingContext context,
                                       Rectangle bounds,
                                       Color color,
                                       float borderThickness,
                                       float fillOpacity,
                                       string labelText,
                                       TextOptions textOptions,
                                       float textPadding)
        {
            var polygon = new RectangularPolygon(bounds);

            context.Draw(color, borderThickness, polygon);

            if (fillOpacity > 0F)
                context.Fill(color.WithAlpha(fillOpacity), polygon);

            var rendered = TextMeasurer.MeasureSize(labelText, textOptions);
            var renderedSize = new Size((int)(rendered.Width + textPadding), (int)rendered.Height);

            var location = bounds.Location;

            location.Offset(0, -renderedSize.Height);

            //var textLocation = new Point((int)(location.X + textPadding / 2), location.Y);
            var textLocation = new PointF(location.X + textPadding / 2, location.Y);

            var textBoxPolygon = new RectangularPolygon(location, renderedSize);

            context.Fill(color, textBoxPolygon);
            context.Draw(color, borderThickness, textBoxPolygon);

            context.DrawText(labelText, textOptions.Font, Color.White, textLocation);
        }
        static string[] ColorPalette = new string[]
            {
                "FF3838",
                "FF9D97",
                "FF701F",
                "FFB21D",
                "CFD231",
                "48F90A",
                "92CC17",
                "3DDB86",
                "1A9334",
                "00D4BB",
                "2C99A8",
                "00C2FF",
                "344593",
                "6473FF",
                "0018EC",
                "8438FF",
                "520085",
                "CB38FF",
                "FF95C8",
                "FF37C7",
                };
        private static Rgba32 getCorlorPalete(int id)
        {
            var hex = ColorPalette[id % ColorPalette.Length];
            return SixLabors.ImageSharp.Color.ParseHex(hex);
        }

        private static SixLabors.ImageSharp.Image CreateContours(this SixLabors.ImageSharp.Image source, SixLabors.ImageSharp.Color color, float thickness)
        {
            var contours = ImageContoursHelper.FindContours(source);

            var result = new Image<Rgba32>(source.Width, source.Height);

            foreach (var points in contours)
            {
                if (points.Count < 2)
                    continue;

                var pathBuilder = new SixLabors.ImageSharp.Drawing.PathBuilder();
                pathBuilder.AddLines(points.Select(x => (SixLabors.ImageSharp.PointF)x));

                var path = pathBuilder.Build();

                result.Mutate(x =>
                {
                    x.Draw(color, thickness, path);
                });
            }

            return result;
        }


    }
}
