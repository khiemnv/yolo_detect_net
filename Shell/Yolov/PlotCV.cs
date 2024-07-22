using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Yolov
{
    public static class PlotCV
    {

        public static void Crop(string img_path,
            List<SegmentationBoundingBox> segs,
            string outPath)
        {
            var originImage = new OpenCvSharp.Mat(img_path);
            segs.ForEach(seg =>
            {
                // draw mask
                var masksLayer = new Mat();
                var contoursLayer = new Mat();

                var mask = new Mat(seg.bounds.Height, seg.bounds.Width, MatType.CV_8UC1);
                for (int x = 0; x < seg.mask.Width; x++)
                {
                    for (int y = 0; y < seg.mask.Height; y++)
                    {
                        var value = seg.mask[x, y];

                        if (value > 0.5)
                            mask.At<Vec3b>(y, x)[0] = 255;
                    }
                }

                //mask.SaveImage("mask2.jpeg");

                Cv2.FindContours(mask, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
                var len = contours.Length;

                var cnt = 0;
                for (int i = 1; i < len; i++)
                {
                    if (contours[i].Length > contours[cnt].Length)
                    {
                        cnt = i;
                    }
                }

                var m = Cv2.Moments(contours[cnt]);

                contoursLayer = new Mat(originImage.Height, originImage.Width, MatType.CV_8UC1);
                Cv2.DrawContours(contoursLayer, contours, cnt, Scalar.White, Cv2.FILLED, LineTypes.Link8, hierarchy, 1, new Point(seg.bounds.Location.X, seg.bounds.Location.Y));
                //contoursLayer.SaveImage("contoursLayer.jpeg");

                var crop = new Mat(originImage.Height, originImage.Width, MatType.CV_8UC3);
                Cv2.CopyTo(originImage, crop, contoursLayer);
                //crop.SaveImage("crop.jpeg");
            });


            originImage.SaveImage(outPath);
        }

        public static List<System.Drawing.PointF> GetContour(SegmentationBoundingBox seg)
        {
            var (_, _, contour) = GetContour(seg.mask);
            return contour.Select(p => new System.Drawing.PointF(p.X + seg.bounds.X, p.Y + seg.bounds.Y)).ToList();
        }

        public static (double angle, double area, Point[] coutour) GetContour(IMask mask)
        {
            var vinoMask = (VinoMask)mask;
            var (width, height) = (vinoMask.Width, vinoMask.Height);
            var newMask = new Mat(height, width, MatType.CV_8UC1);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var value = vinoMask.GetConfidence(x, y);

                    if (value > 0.5)
                    {
                        newMask.At<Vec3b>(y, x)[0] = 255;
                    }
                }
            }

            Cv2.FindContours(newMask,
                out Point[][] contours,
                out _,
                RetrievalModes.List,
                ContourApproximationModes.ApproxSimple);

            // max contour
            var len = contours.Length;
            var conIdx = 0;
            for (int i = 1; i < len; i++)
            {
                if (contours[i].Length > contours[conIdx].Length)
                {
                    conIdx = i;
                }
            }
            var contour = contours[conIdx];

            // approximate
            var ep = 15;
            var approx = Cv2.ApproxPolyDP(contour, ep, true);
            contour = approx;

            var me = Cv2.MinAreaRect(contour);
            var angle = me.Angle;
            if (angle > 80 && angle < 100)
            {
                angle -= 90;
            }

            var area = Cv2.ContourArea(contour);
            return (angle, area, contour);
        }

        public static (double, double) Crop(string img_path,
            IMask mask,
            (int x, int y, int width, int height) bounds,
            string outPath = null)
        {
            var originImage = new OpenCvSharp.Mat(img_path);
            var vinoMask = (VinoMask)mask;
            var newMask = new Mat(bounds.height, bounds.width, MatType.CV_8UC1);
            for (int x = 0; x < bounds.width; x++)
            {
                for (int y = 0; y < bounds.height; y++)
                {
                    var value = vinoMask.mask.At<Vec3b>(y, x)[0];

                    if (YoloModelBase.GetConfidence(value) > 0.5)
                        newMask.At<Vec3b>(y, x)[0] = 255;
                }
            }

            Cv2.FindContours(newMask,
                out Point[][] contours,
                out HierarchyIndex[] hierarchy,
                RetrievalModes.List,
                ContourApproximationModes.ApproxSimple);

            // max contour
            var len = contours.Length;
            var conIdx = 0;
            for (int i = 1; i < len; i++)
            {
                if (contours[i].Length > contours[conIdx].Length)
                {
                    conIdx = i;
                }
            }
            var contour = contours[conIdx];

            //var hull = Cv2.ConvexHull(contours[conIdx]);
            //var hulls = new Point[][] { hull };

            var ep = 15;
            var approx = Cv2.ApproxPolyDP(contour, ep, true);
            contour = approx;

            var mu = Cv2.Moments(contour);
            var (cx, cy) = (mu.M10 / mu.M00 + 1e-5, mu.M01 / mu.M00 + 1e-5);

            //crop.SaveImage(outPath);
            var me = Cv2.MinAreaRect(contour);

            Mat contoursLayer = new Mat(originImage.Height, originImage.Width, MatType.CV_8UC1);
            Cv2.DrawContours(contoursLayer, new Point[][] { contour }, 0,
               Scalar.White, Cv2.FILLED, LineTypes.Link8,
               null, 1, new Point(bounds.x, bounds.y));

            //contoursLayer.SaveImage("contoursLayer.jpeg");

            var crop = new Mat(originImage.Height, originImage.Width, MatType.CV_8UC3);
            Cv2.CopyTo(originImage, crop, contoursLayer);

            Point[] points = Array.ConvertAll(me.Points(), p => new Point(p.X + bounds.x, p.Y + bounds.y));
            Cv2.Polylines(crop, new Point[][] { points }, true, Scalar.Red, 10);
            Cv2.Circle(crop, (int)cx + bounds.x, (int)cy + bounds.y, 5, Scalar.Red, 10);

            if (!string.IsNullOrEmpty(outPath))
            {
                crop.SaveImage(outPath);
            }

            var angle = me.Angle;
            if (angle > 80 && angle < 100)
            {
                angle -= 90;
            }

            var area = Cv2.ContourArea(contour);
            return (angle, area);
            //return mu.M01 / (mu.M00 + 1e-5);
        }

        public static (double angle, double area, double cx, double cy) GetContourInfo(IEnumerable<Point> contour)
        {
            var mu = Cv2.Moments(contour);
            var (cx, cy) = (mu.M10 / mu.M00 + 1e-5, mu.M01 / mu.M00 + 1e-5);

            var me = Cv2.MinAreaRect(contour);
            var angle = me.Angle;
            if (angle > 80 && angle < 100)
            {
                angle -= 90;
            }

            var area = Cv2.ContourArea(contour);
            return (angle, area, cx, cy);
        }
    }
}