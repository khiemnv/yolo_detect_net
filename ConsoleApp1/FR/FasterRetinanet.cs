using ConsoleApp1;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Yolov;

namespace FR
{
    public class FasterRetinanet: YoloModelBase
    {
        private string ModelInputName;

        protected override void ReadModel(string model_path)
        {
            _inferenceSession = new InferenceSession(File.ReadAllBytes(model_path));

            var path = Path.GetDirectoryName(model_path) + "\\names.txt";
            var names = File.ReadAllText(path);
            labelDict = ParseNames(names);
            labels = labelDict.ToList().ConvertAll(p => new PredictionBox.Label { Id = p.Key, Name = p.Value });

            ModelInputName = _inferenceSession.InputNames.FirstOrDefault();
        }
        private Tensor<byte> ExtractPixels(Image<Rgba32> image)
        {
            var tensor = new DenseTensor<byte>(new[] { 1, image.Height, image.Width, 3 });

            Parallel.For(0, image.Height, y =>
            {
                Parallel.For(0, image.Width, x =>
                {
                    tensor[0, y, x, 0] = image[x, y].R; // r
                    tensor[0, y, x, 1] = image[x, y].G; // g
                    tensor[0, y, x, 2] = image[x, y].B; // b
                });
            });

            return tensor;
        
        }

        private Tensor<byte> ExtractPixels(System.Drawing.Image image)
        {
            var bitmap = (Bitmap)image;

            var rectangle = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, bitmap.PixelFormat);
            int bytesPerPixel = System.Drawing.Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;

            var tensor = new DenseTensor<byte>(new[] { 1, bitmap.Height, bitmap.Width, 3 });

            unsafe // speed up conversion by direct work with memory
            {
                Parallel.For(0, bitmapData.Height, (y) =>
                {
                    byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);

                    Parallel.For(0, bitmapData.Width, (x) =>
                    {
                        tensor[0, y, x, 0] = row[x * bytesPerPixel + 0];
                        tensor[0, y, x, 1] = row[x * bytesPerPixel + 1];
                        tensor[0, y, x, 2] = row[x * bytesPerPixel + 2];
                    });
                });

                bitmap.UnlockBits(bitmapData);
            }

            return tensor;
        }

        //public override List<PredictionBox> Detect(string img_path, string model_path)
        //{
        //    if (!string.IsNullOrEmpty(model_path))
        //    {
        //        ReadModel(model_path);
        //    }
        //    if (!string.IsNullOrEmpty(img_path))
        //    {
        //        //var image = new Bitmap(img_path);
        //        var image = SixLabors.ImageSharp.Image.Load<Rgba32>(img_path);
        //        int w = image.Width;
        //        int h = image.Height;

        //        var outputs = Inference(image);
        //        var raw = ParseDetect(outputs[0], w, h);

        //        return raw;
        //    }
        //    return null;
        //}
        private List<DenseTensor<float>> Inference(Bitmap image)
        {
            var tensor = ExtractPixels(image);
            var inputs = new List<NamedOnnxValue> // add image as onnx input
            {
                NamedOnnxValue.CreateFromTensor(ModelInputName, tensor)
            };
            return Inference(inputs);
        }

        private List<DenseTensor<float>> Inference(List<NamedOnnxValue> inputs)
        {
            var result = _inferenceSession.Run(inputs);
            var names = new List<string> { "detection_boxes", "detection_classes", "detection_scores" };
            var raw = names.ConvertAll(name =>
            {
                return result.First(x => x.Name == name).Value as DenseTensor<float>;
            });

            raw = Conv2yolovOutput(raw);

            return raw;
        }

        protected override List<DenseTensor<float>> Inference(Image<Rgba32> image)
        {
            var tensor = ExtractPixels(image);
            var inputs = new List<NamedOnnxValue> // add image as onnx input
            {
                NamedOnnxValue.CreateFromTensor(ModelInputName, tensor)
            };
            return Inference(inputs);
        }

        // box_x, box_y, box_w, box_h, confidence, class index
        private List<DenseTensor<float>> Conv2yolovOutput(List<DenseTensor<float>> lst)
        {
            ModelOutputDimensions = 6;
            var boxes = lst[0]; // [1,50,4]  xmin ymin xmax ymax
            var label = lst[1]; // [1,50]
            var score = lst[2]; // [1,50]
            int n = (int)score.Length;
            var output = new DenseTensor<float>(new[] { 1, 6, n });
            Parallel.For(0, n, (i) => {
                output[0, 0, i] = boxes[0, i, 0];
                output[0, 1, i] = boxes[0, i, 1];
                output[0, 2, i] = boxes[0, i, 2];
                output[0, 3, i] = boxes[0, i, 3];
                output[0, 4, i] = score[0, i];
                output[0, 5, i] = label[0, i];
            });
            return new List< DenseTensor<float> > { output};
        }

        // [1,6,n]
        protected override List<PredictionBox> ParseDetect(DenseTensor<float> output, int w, int h)
        {
            var result = new ConcurrentBag<PredictionBox>();
            Parallel.For(0, (int)output.Length / ModelOutputDimensions, (i) =>
            {
                var confidence = output[0, 4, i];
                if (confidence <= ModelConfidence) return; // skip low obj_conf results

                var xMin = output[0, 1, i] * w;
                var yMin = output[0, 0, i] * h;
                var xMax = output[0, 3, i] * w;
                var yMax = output[0, 2, i] * h;

                xMin = Clamp(xMin, 0, w - 0); // clip bbox tlx to boundaries
                yMin = Clamp(yMin, 0, h - 0); // clip bbox tly to boundaries
                xMax = Clamp(xMax, 0, w - 1); // clip bbox brx to boundaries
                yMax = Clamp(yMax, 0, h - 1); // clip bbox bry to boundaries

                var k = (int)output[0, 5, i];
                PredictionBox.Label label = labels[k-1]; // one base

                var prediction = new PredictionBox()
                {
                    label = label,
                    Score = confidence,
                    Rectangle = new SixLabors.ImageSharp.RectangleF(xMin, yMin, xMax - xMin, yMax - yMin),
                    Index = i,
                };

                result.Add(prediction);
            });
            
            return result.ToList();
        }
    }
}
