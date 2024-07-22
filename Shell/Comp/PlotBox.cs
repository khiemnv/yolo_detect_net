using annotation;
using System;
using System.Collections.Generic;
using System.Drawing;
using Yolov;

namespace comp
{
    public class PlotBox
    {
        public void Export(string img_path, List<PredictionBox> boxes, string img_out)
        {
            // label
            boxes.ForEach(p =>
            {
                var txt = p.label.name;
            });

            // draw
            try
            {
                var scale = BaseConfig.GetBaseConfig().fontScale;

                var bmp = new Bitmap(img_path);
                CreateResultImg(bmp, new Font("Arial", bmp.Height / scale), boxes);
                bmp.Save(img_out);
            }
            catch (Exception e)
            {
                Logger.Logger.Error(e.Message);
            }

        }
        public void CreateResultImg<T>(Bitmap btm, Font font, List<T> boxes)
            where T : PredictionBox
        {
            if (boxes == null) { return; }
            var imgSize = (btm.Width, btm.Height);
            Graphics G = Graphics.FromImage(btm);
            var labelRectLst = new List<Rectangle>();
            foreach (var box in boxes)
            {
                // draw label in original images
                var rect = box.rectangle;
                var label = $"{box.label.name} {box.score:0.00}";
                Size sz = System.Windows.Forms.TextRenderer.MeasureText(label, font);
                var labelRect = AdjLabelPos(new Rectangle((int)rect.X, (int)rect.Y, sz.Width, sz.Height), imgSize, labelRectLst);
                var color = getCorlor(box.label.id);
                Pen pen = new Pen(color, font.Size / 5);
                {
                    G.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    color = new SolidBrush(Color.FromArgb(80, ((SolidBrush)color).Color));
                    G.FillRectangle(color, labelRect);
                    G.DrawString(label, font, new SolidBrush(Color.White), labelRect.X, labelRect.Y);
                }
            }
        }
        private Rectangle AdjLabelPos(Rectangle rectangle, (int Width, int Height) imgSize, List<Rectangle> labelRectLst)
        {
            rectangle.Y -= rectangle.Height;

            // check overlap
            foreach (var rec in labelRectLst)
            {
                var intersect = Rectangle.Intersect(rectangle, rec);
                if (!intersect.IsEmpty)
                {
                    if (rectangle.X > rec.X)
                        rectangle.X += intersect.Width;
                    else
                        rectangle.X -= intersect.Width;
                }
            }

            // add to list
            labelRectLst.Add(rectangle);
            return rectangle;
        }

        List<Brush> solidColorBrushList = new List<Brush>()
                {
                     new SolidBrush(System.Drawing.Color.FromArgb(255,27,161,226)),
                     new SolidBrush(System.Drawing.Color.FromArgb(255,160,80,0)),
                     new SolidBrush(System.Drawing.Color.FromArgb(255, 51, 153, 51)),
                     new SolidBrush(System.Drawing.Color.FromArgb(255, 162, 193, 57)),
                     new SolidBrush(System.Drawing.Color.FromArgb(255, 216, 0, 115)),
                     new SolidBrush(System.Drawing.Color.FromArgb(255, 240, 150, 9)),
                     new SolidBrush(System.Drawing.Color.FromArgb(255, 230, 113, 184)),
                     new SolidBrush(System.Drawing.Color.FromArgb(255, 162, 0, 255)),
                     new SolidBrush(System.Drawing.Color.FromArgb(255, 229, 20, 0)),
                     new SolidBrush(System.Drawing.Color.FromArgb(255, 0, 171, 169))
                };
        private Brush getCorlor(int id)
        {
            return solidColorBrushList[id % solidColorBrushList.Count];
        }
    }
}
