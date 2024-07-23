using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Yolov;

namespace FR
{
    public class FasterRetinanet : YoloModelBase
    {
        private string ModelInputName;

        public static List<string> ParsePbtxt(string path)
        {
            // parse
            var dict = FasterRetinanet.ReadLabelDict(path);
            return dict.Values.ToList();
        }

        protected override void ReadModel(string model_path)
        {
            DDShell.FRReadModel(this, model_path);
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
#if UNSAFE
        private Tensor<byte> ExtractPixels(string imagePath)
        {
            var bitmap = new System.Drawing.Bitmap(imagePath);

            var rectangle = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(rectangle, System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
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
#endif
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
        protected override List<DenseTensor<float>> Inference(Image<Rgba32> image)
        {
            var tensor = ExtractPixels(image);
            var inputs = new List<NamedOnnxValue> // add image as onnx input
            {
                NamedOnnxValue.CreateFromTensor("input_tensor", tensor)
            };

            var result = _inferenceSession.Run(inputs);
            var names = new List<string> { "detection_boxes", "detection_classes", "detection_scores" };
            var raw = names.ConvertAll(name =>
            {
                return result.First(x => x.Name == name).Value as DenseTensor<float>;
            });

#if false
            var raw_detection_boxes = result.First(x=>x.Name == "raw_detection_boxes").Value as DenseTensor<float>;
            var rdb = raw_detection_boxes.ToArray();
            for (var i = 0;i<50*4;)
            {
                Console.WriteLine($"{rdb[i+0]} {rdb[i+1]} {rdb[i+2]} {rdb[i+3]}");
                i += 4;
            }

            var raw_detection_scores = (result.First(x => x.Name == "raw_detection_scores").Value as DenseTensor<float>);
            var rds = raw_detection_scores.ToArray();
            for (var i = 0; i < 50 * 33;)
            {
                for (var j = 0;j<33;j++)
                {
                    Console.Write($"{rds[i + j]} ");
                }
                Console.WriteLine();
                i += 33;
            }
#endif

            //raw = Conv2yolovOutput(raw);

            return raw;
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
            Parallel.For(0, n, (i) =>
            {
                output[0, 0, i] = boxes[0, i, 0];
                output[0, 1, i] = boxes[0, i, 1];
                output[0, 2, i] = boxes[0, i, 2];
                output[0, 3, i] = boxes[0, i, 3];
                output[0, 4, i] = score[0, i];
                output[0, 5, i] = label[0, i];
            });
            return new List<DenseTensor<float>> { output };
        }

        // [1,6,n]
        protected override List<PredictionBox> ParseDetect(DenseTensor<float> output, int w, int h)
        {
            throw new NotImplementedException();
        }
        protected override List<PredictionBox> ParseDetect(List<DenseTensor<float>> outputs, int w, int h)
        {
            var t_boxes = outputs[0]; // [1,50,4]  xmin ymin xmax ymax
            var t_labelIdxs = outputs[1]; // [1,50]
            var t_scores = outputs[2]; // [1,50]

            var result = new ConcurrentBag<PredictionBox>();
            Parallel.For(0, (int)t_labelIdxs.Length, (i) =>
            {
                var confidence = t_scores[0, i];
                if (confidence <= ModelConfidence) return; // skip low obj_conf results

                var xMin = t_boxes[0, i, 1] * w;
                var yMin = t_boxes[0, i, 0] * h;
                var xMax = t_boxes[0, i, 3] * w;
                var yMax = t_boxes[0, i, 2] * h;

                xMin = Clamp(xMin, 0, w - 0); // clip bbox tlx to boundaries
                yMin = Clamp(yMin, 0, h - 0); // clip bbox tly to boundaries
                xMax = Clamp(xMax, 0, w - 1); // clip bbox brx to boundaries
                yMax = Clamp(yMax, 0, h - 1); // clip bbox bry to boundaries

                var k = (int)t_labelIdxs[0, i];
                PredictionBox.Label label = labels[k - 1]; // one base

                var prediction = new PredictionBox()
                {
                    LabelName = label.name,
                    score = confidence,
                    rectangle = new PredictionBox.MyRect(xMin, yMin, xMax - xMin, yMax - yMin),
                    index = i,
                };

                result.Add(prediction);
            });

            return result.ToList();
        }

        public static Dictionary<int, string> ReadLabelDict(string labelMap)
        {
            var dict = new Dictionary<int, string>();
            var txt = File.ReadAllText(labelMap);
            var m = Regex.Matches(txt, @"item {[\s\r\n]+id: (\d+)[\s\r\n]+name: '(\w+)'[\s\r\n]+}", RegexOptions.Multiline);
            foreach (Match i in m)
            {
                Debug.Assert(dict.Count == (int.Parse(i.Groups[1].Value) - 1));
                dict.Add(dict.Count, i.Groups[2].Value);
            }
            return dict;
        }

#if UNSAFE
        public override PredictionBoxCollection DetectUnsafe(string imgPath)
        {
            var env = this;
            var (ts, imgSize) = DDShell.FRExtractPixelsUnsafe(env, imgPath);
            var output = DDShell.FRInference(env, ts);
            var raw = DDShell.FRParseDetect(env, output, imgSize.Width, imgSize.Height);
            var boxes = DDShell.Suppress(env, raw);
            return new PredictionBoxCollection { w = imgSize.Width, h = imgSize.Height, boxes = boxes };
        }
#endif
    }
}
