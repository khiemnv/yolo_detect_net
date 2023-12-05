
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
            string model_path = args[0]; // .onnx
            string img_path = args[1];


#if true
            var yolo = new YoloModel8
            {
                Overlap = 0.45f,
                ModelConfidence = 0.6f,
            };

            List<SegmentationBoundingBox> segs = yolo.SegDetect(img_path, model_path);
            var outPath = Path.GetFileNameWithoutExtension(img_path) + "_o.jpeg";
            Plot.Export(img_path, segs, outPath);

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

    }
}
