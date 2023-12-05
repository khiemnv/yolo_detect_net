using System.Drawing;
using System;
using System.Collections.Generic;
using Yolov;
using System.IO;

namespace ConsoleApp1
{
    public class PlotBox
    {
        public void Export(string img_path, List<PredictionBox> boxes, string img_out)
        {
            // label
            boxes.ForEach(p =>
            {
                var txt = p.label.Name;
            });

            // draw
            try
            {
                var bmp = new Bitmap(img_path);
                CreateResultImg(bmp, new Font("Arial", bmp.Height / 100), boxes);
                bmp.Save(img_out);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
            }

        }
        private void CreateResultImg(Bitmap btm, Font font, List<PredictionBox> outObjs)
        {
            outObjs.ForEach(outObj =>
            {
                // draw label in original images
                var rect = outObj.Rectangle;
                var label = outObj.label.Name;
                var color = getCorlor(outObj.label.Id);
                Pen pen = new Pen(color, font.Size / 10);
                using (Graphics G = Graphics.FromImage(btm))
                {
                    G.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    G.DrawString($"{label} {outObj.Score:0.00}", font, color, rect.X, rect.Y);
                }
            });
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
        private  Brush getCorlor(int id)
        {
            return solidColorBrushList[id % solidColorBrushList.Count];
        }
    }
}
