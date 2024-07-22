
using ClosedXML.Excel;
using FR;
using Newtonsoft.Json;
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
            Console.WriteLine("<model_path> <in_dir> <model_type> <out_dir>");
            string model_path = args[0]; // .onnx
            string in_dir = args[1];
            string model_type = args.Length > 2 ? args[2] : nameof(YoloModel8);
            string out_dir = args.Length > 3 ? args[3] : Path.GetDirectoryName(model_path) + "\\output\\" + Path.GetFileName(in_dir);

            YoloModelBase yolo = null;
            switch (model_type)
            {
                case nameof(YoloModel5):
                    yolo = new YoloModel5()
                    {
                        Overlap = 0.45f,
                        ModelConfidence = 0.6f,
                        MulConfidence = 0.6f,
                    }; break;
                case nameof(YoloModel8):
                    yolo = new YoloModel8
                    {
                        Overlap = 0.45f,
                        ModelConfidence = 0.6f,
                    }; break;
                default:
                    yolo = new FasterRetinanet()
                    {
                        Overlap = 0.45f,
                        ModelConfidence = 0.6f,
                    }; break;
            }

            if (!Directory.Exists(out_dir)) { Directory.CreateDirectory(out_dir); }
            DetectImgs(model_path, in_dir, out_dir, yolo);
            return;

            //List<PredictionBox> boxes = fr.Detect(img_path, model_path);
            //new PlotBox().Export(img_path, boxes, Path.GetFileNameWithoutExtension(img_path) + "_o.jpeg");
            //return;
#if true


            // create expect result
            //model_path = @"C:\work\projects\doorobjdetector\models_comp\models\yolov8x\best.onnx";
            //var input_dir = "C:\\work\\projects\\doorobjdetector\\models_comp\\test\\front_ng";
            //CreateExpect(model_path, input_dir, yolo);

            //var out_dir = Path.GetDirectoryName(model_path) + "\\output\\" + Path.GetFileNameWithoutExtension(input_dir);
            //if (!Directory.Exists(out_dir)) { Directory.CreateDirectory(out_dir); }
            //DetectImgs(model_path, input_dir, out_dir, fr);
            //model_path = @"C:\work\projects\doorobjdetector\models_comp\models\yolov8x\best.onnx";
            
            //yolo.Detect(null, model_path);
            //var outPath = Path.GetFileNameWithoutExtension(img_path) + "_o.jpeg";
            ////List<SegmentationBoundingBox> segs = yolo.SegDetect(img_path, model_path);
            ////Plot.Export(img_path, segs, outPath);

            //model_path = @"C:\work\projects\doorobjdetector\models_comp\models\yolov5s\yolo.onnx";
            //var yolo = new YoloModel5
            //{
            //    Overlap = 0.45f,
            //    ModelConfidence = 0.6f,
            //    MulConfidence = 0.6f,
            //};
            //img_path = @"C:\work\projects\doorobjdetector\models_comp\test\back_ng\10344b20331c2000f3b894cf1410e5d3_219.jpeg";
            //var boxes = yolo.Detect(img_path, model_path);
            //new PlotBox().Export(img_path, boxes, Path.GetFileNameWithoutExtension(img_path) + "_o.jpeg");

            return;
#else
            var yolo = new YoloModel5
            {
                Overlap = 0.45f,

                ModelConfidence = 0.6f,
                MulConfidence = 0.6f,
            };
            List<PredictionBox> boxes = yolo.Detect(img_path, model_path);
            new PlotBox().Export(img_path, boxes, Path.GetFileNameWithoutExtension(img_path) + "_o.jpeg");
#endif       
        }

        private static void DetectImgs5(string model_path, string input_dir, string out_dir)
        {
            var yolo = new YoloModel5
            {
                Overlap = 0.45f,
                ModelConfidence = 0.6f,
            };
            DetectImgs(model_path, input_dir, out_dir, yolo);
        }

        private static void DetectImgs(string model_path, string input_dir, string out_dir, YoloModelBase yolo)
        {
            yolo.Detect(null, model_path);

            var di = new DirectoryInfo(input_dir);
            di.GetFiles("*.jpeg").ToList().ForEach(file =>
            {
                var lst = yolo.Detect(file.FullName, null);
                var path = Path.Combine(out_dir, Path.GetFileNameWithoutExtension(file.Name) + ".txt");
                var obj = lst.ConvertAll(b => new
                {
                    label = b.label.Name,
                    score = b.Score,
                    x = b.Rectangle.X,

                    y = b.Rectangle.Y,
                    w = b.Rectangle.Width,
                    h = b.Rectangle.Height,
                });
                var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
                File.WriteAllText(path, json);
            });
        }

        private static void CreateExpect(string model_path, string input_dir, YoloModelBase yolo)
        {
            var di = new DirectoryInfo(input_dir);
            var out_dir = input_dir;
            DetectImgs(model_path, input_dir, out_dir, yolo);
        }
    }
}
