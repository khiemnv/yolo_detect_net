using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Yolov;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string img_path = args[1];
            string model_path = args[0]; // .onnx

            var yolo = new YoloModel5
            {
                Overlap = 0.45f,

                ModelConfidence = 0.6f,
                MulConfidence = 0.6f,
            };

            //var yolo = new YoloModel8
            //{
            //    Overlap = 0.45f,
            //    ModelConfidence = 0.6f,
            //};

            var retsult = yolo.Detect(img_path, model_path);
            labelConfig._dict = yolo.labelDict;
            // label
            retsult.ForEach(p =>
            {
                var txt = p.label.Id.ToString();
                if (labelConfig._dict.ContainsKey(p.label.Id))
                {
                    txt = labelConfig._dict[p.label.Id];
                    if (labelConfig._dict2.ContainsKey(txt))
                    {
                        txt = labelConfig._dict2[txt];
                    }
                }
                p.label.Name = txt;
            });

            // draw
            try
            {
                var bmp = new Bitmap(img_path);
                CreateResultImg(bmp, new Font("Arial", bmp.Height / 100), retsult);
                bmp.Save(Path.GetFileNameWithoutExtension(img_path) + "_o.jpeg");
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
            }

        }
        private static void CreateResultImg(Bitmap btm, Font font, List<PredictionBox> outObjs)
        {
            outObjs.ForEach(outObj =>
            {
                // draw label in original images
                var rect = outObj.Rectangle;
                var label = outObj.label.Name;
                var color = getCorlor(label);
                Pen pen = new Pen(color, font.Size / 10);
                using (Graphics G = Graphics.FromImage(btm))
                {
                    G.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    G.DrawString($"{label} {outObj.Score:0.00}", font, color, rect.X, rect.Y);
                }
            });
        }

        static LabelConfig labelConfig = new LabelConfig();
        private static Brush getCorlor(string name)
        {
            if (labelConfig.d == null)
            {
                var solidColorBrushList = new List<Brush>()
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
                labelConfig.d = new Dictionary<string, Brush>();
                labelConfig._dict.ToList().ForEach(p => labelConfig.d[p.Value] = solidColorBrushList[(int)p.Key % solidColorBrushList.Count]);
                labelConfig._dict2.ToList().ForEach(p => labelConfig.d[p.Value] = labelConfig.d[p.Key]);
            }


            if (labelConfig.d.ContainsKey(name))
                return labelConfig.d[name];

            return Brushes.Red;
        }
    }
}
