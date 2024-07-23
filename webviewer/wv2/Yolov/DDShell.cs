using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Yolov
{
    public class DDShell
    {
        public static Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> ExtractPixels(
            DDShellEnv env,
            SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image)
        {
            var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(new[] { 1, 3, env.ModelInputHeight, env.ModelInputWidth });

            Parallel.For(0, image.Height, y =>
            {
                Parallel.For(0, image.Width, x =>
                {
                    tensor[0, 0, y, x] = image[x, y].R / 255.0F; // r
                    tensor[0, 1, y, x] = image[x, y].G / 255.0F; // g
                    tensor[0, 2, y, x] = image[x, y].B / 255.0F; // b
                });
            });

            return tensor;
        }
        public static (Microsoft.ML.OnnxRuntime.Tensors.Tensor<float>, (int Width, int Height)) ExtractPixels(
            DDShellEnv env,
            string imagePath)
        {
            var (Width, Height) = (env.ModelInputWidth, env.ModelInputHeight);
            var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(imagePath);
            var imageSize = (image.Width, image.Height);
            if (image.Width != Width || image.Height != Height)
            {
                image.Mutate(x => x.Resize(Width, Height)); // fit image size to specified input size
            }
            return (ExtractPixels(env, image), imageSize);
        }

        //public static Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> ExtractPixels2(
        //    DDShellEnv env,
        //    string imageFilePath)
        //{
        //    using (SixLabors.ImageSharp.Image<Rgb24> image = SixLabors.ImageSharp.Image.Load<Rgb24>(imageFilePath/*, out IImageFormat format*/))
        //    {
        //        using (System.IO.Stream imageStream = new System.IO.MemoryStream())
        //        {
        //            image.Mutate(x =>
        //            {
        //                x.Resize(new ResizeOptions
        //                {
        //                    Size = new SixLabors.ImageSharp.Size(env.ModelInputHeight, env.ModelInputWidth),
        //                    Mode = ResizeMode.Stretch
        //                });
        //            });
        //            image.Save(imageStream, format);

        //            var mean = new[] { 0.485f, 0.456f, 0.406f };
        //            var stddev = new[] { 0.229f, 0.224f, 0.225f };
        //            DenseTensor<float> processedImage = new DenseTensor<float>(new[] { 1, 3, env.ModelInputHeight, env.ModelInputWidth });
        //            image.ProcessPixelRows(accessor =>
        //            {
        //                for (int y = 0; y < accessor.Height; y++)
        //                {
        //                    Span<Rgb24> pixelSpan = accessor.GetRowSpan(y);
        //                    for (int x = 0; x < accessor.Width; x++)
        //                    {
        //                        processedImage[0, 0, y, x] = (pixelSpan[x].R / 255f);
        //                        processedImage[0, 1, y, x] = (pixelSpan[x].G / 255f);
        //                        processedImage[0, 2, y, x] = (pixelSpan[x].B / 255f);
        //                    }
        //                }
        //            });
        //            return processedImage;
        //        }
        //    }
        //}

#if UNSAFE
        public static (Microsoft.ML.OnnxRuntime.Tensors.Tensor<float>, (int Width, int Height)) ExtractPixelsUnsafe(DDShellEnv env,
            string imageFilePath)
        {
            var org = new System.Drawing.Bitmap(imageFilePath);
            var imageSize = (org.Width, org.Height);
            var bitmap = new System.Drawing.Bitmap(org, env.ModelInputWidth, env.ModelInputHeight);

            var rectangle = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(rectangle, System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
            int bytesPerPixel = System.Drawing.Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;

            var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(new[] { 1, 3, bitmap.Height, bitmap.Width });

            unsafe // speed up conversion by direct work with memory
            {
                Parallel.For(0, bitmapData.Height, (y) =>
                {
                    byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);

                    Parallel.For(0, bitmapData.Width, (x) =>
                    {
                        tensor[0, 0, y, x] = row[x * bytesPerPixel + 0] / 255f;
                        tensor[0, 1, y, x] = row[x * bytesPerPixel + 1] / 255f;
                        tensor[0, 2, y, x] = row[x * bytesPerPixel + 2] / 255f;
                    });
                });

                bitmap.UnlockBits(bitmapData);
            }

            return (tensor, imageSize);
        }
#endif

        public static List<Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>> Inference(DDShellEnv env, Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> tensor)
        {
            var _inferenceSession = env._inferenceSession;

            var inputs = new List<Microsoft.ML.OnnxRuntime.NamedOnnxValue> // add image as input
            {
                Microsoft.ML.OnnxRuntime.NamedOnnxValue.CreateFromTensor("images", tensor)
            };
            var result = _inferenceSession.Run(inputs);
            var raw = result.Select(i => i.Value as Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>).ToList();
            return raw;
        }
        public static List<DenseTensor<float>> FRInference(DDShellEnv env, Tensor<byte> tensor)
        {
            var inputs = new List<NamedOnnxValue> // add image as onnx input
            {
                NamedOnnxValue.CreateFromTensor("input_tensor", tensor)
            };
            var result = env._inferenceSession.Run(inputs);
            var names = new List<string> { "detection_boxes", "detection_classes", "detection_scores" };
            var raw = names.ConvertAll(name =>
            {
                return result.First(x => x.Name == name).Value as DenseTensor<float>;
            });
            return raw;
        }

        public static Tensor<byte> FRExtractPixels(DDShellEnv env, string imagePath)
        {
            using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imagePath))
            {
                return FRExtractPixels(env, image);
            }
        }

        public static Tensor<byte> FRExtractPixels(DDShellEnv env, SixLabors.ImageSharp.Image<Rgba32> image)
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
        public static (Tensor<byte>, (int Width, int Height)) FRExtractPixelsUnsafe(DDShellEnv env, string imagePath)
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

            return (tensor, (bitmap.Width, bitmap.Height));
        }
#endif

        public static List<PredictionBox> FRParseDetect(DDShellEnv env, List<DenseTensor<float>> outputs, int w, int h)
        {
            var t_boxes = outputs[0]; // [1,50,4]  xmin ymin xmax ymax
            var t_labelIdxs = outputs[1]; // [1,50]
            var t_scores = outputs[2]; // [1,50]

            var result = new ConcurrentBag<PredictionBox>();
            Parallel.For(0, (int)t_labelIdxs.Length, (i) =>
            {
                var confidence = t_scores[0, i];
                if (confidence <= env.ModelConfidence) return; // skip low obj_conf results

                var xMin = t_boxes[0, i, 1] * w;
                var yMin = t_boxes[0, i, 0] * h;
                var xMax = t_boxes[0, i, 3] * w;
                var yMax = t_boxes[0, i, 2] * h;

                xMin = Clamp(xMin, 0, w - 0); // clip bbox tlx to boundaries
                yMin = Clamp(yMin, 0, h - 0); // clip bbox tly to boundaries
                xMax = Clamp(xMax, 0, w - 1); // clip bbox brx to boundaries
                yMax = Clamp(yMax, 0, h - 1); // clip bbox bry to boundaries

                var k = (int)t_labelIdxs[0, i];
                PredictionBox.Label label = env.labels[k - 1]; // one base

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

        public static void ReadModel(DDShellEnv env, string model_path)
        {
            var _inferenceSession = CreateSection(env.DeviceDetect, model_path);

            var modelOutputs = _inferenceSession.OutputMetadata.Keys.ToArray();
            var modelInputs = _inferenceSession.InputMetadata.Keys.ToList();
            var names = _inferenceSession.ModelMetadata.CustomMetadataMap["names"];
            env.labelDict = ParseNames(names);
            env.labels = env.labelDict.ToList().ConvertAll(p => new PredictionBox.Label { id = p.Key, name = p.Value });
            env.ModelClassesCount = env.labelDict.Count;
            env.ModelInputWidth = _inferenceSession.InputMetadata[modelInputs[0]].Dimensions[2];
            env.ModelInputHeight = _inferenceSession.InputMetadata[modelInputs[0]].Dimensions[3];
            env.ModelOutputDimensions = _inferenceSession.OutputMetadata[modelOutputs[0]].Dimensions[1]; // [1,38,8400]
            env._inferenceSession = _inferenceSession;
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
        public static void FRReadModel(DDShellEnv env, string model_path)
        {
            env._inferenceSession = CreateSection(env.DeviceDetect, model_path);

            var path = Path.GetDirectoryName(model_path) + "\\names.txt";

            var labelMap = Path.GetDirectoryName(model_path) + "\\label_map.pbtxt";
            if (File.Exists(labelMap))
            {
                env.labelDict = ReadLabelDict(labelMap);
            }
            else
            {
                throw new Exception("Label info not found!");
            }

            env.labels = env.labelDict.ToList().ConvertAll(p => new PredictionBox.Label { id = p.Key, name = p.Value });
        }

        private static InferenceSession CreateSection(string device, string model_path)
        {
            if (device == "cuda")
            {
                SessionOptions options = new SessionOptions();
                options.AppendExecutionProvider_CUDA(0);
                return new InferenceSession(File.ReadAllBytes(model_path), options);
            }
            else
            {
                return new InferenceSession(File.ReadAllBytes(model_path));
            }
        }

        private static Dictionary<int, string> ParseNames(string names)
        {
            var dict = new Dictionary<int, string>();
            var obj = JObject.Parse(names);
            foreach (var idx in obj.Properties())
            {
                dict.Add(int.Parse(idx.Name), idx.Value.ToString());
            }
            return dict;
        }


        public static List<PredictionBox> ParseDetect(DDShellEnv env, DenseTensor<float> output, int w, int h)
        {
            var result = new ConcurrentBag<PredictionBox>();

            var (xGain, yGain) = (env.ModelInputWidth / (float)w, env.ModelInputHeight / (float)h); // x, y gains
            var (xPad, yPad) = ((env.ModelInputWidth - w * xGain) / 2, (env.ModelInputHeight - h * yGain) / 2); // left, right pads

            // [1,38,8400]
            // i=0~8400
            Parallel.For(0, (int)output.Length / env.ModelOutputDimensions, (i) =>
            {

                //Logger.Logger.Debug($"{output[0, i, 0]} {output[0, i, 1]} {output[0, i, 2]} {output[0, i, 3]} {output[0, i, 4]}");
                // j=0~1
                Parallel.For(0, env.ModelClassesCount, (k) =>
                {
                    // ...      [4,     5]
                    // x,y,w,h, class0, class1
                    var confidence = output[0, k + 4, i];
                    if (confidence <= env.ModelConfidence) return; // skip low obj_conf results

                    float xMin = ((output[0, 0, i] - output[0, 2, i] / 2) - xPad) / xGain; // unpad bbox tlx to original
                    float yMin = ((output[0, 1, i] - output[0, 3, i] / 2) - yPad) / yGain; // unpad bbox tly to original
                    float xMax = ((output[0, 0, i] + output[0, 2, i] / 2) - xPad) / xGain; // unpad bbox brx to original
                    float yMax = ((output[0, 1, i] + output[0, 3, i] / 2) - yPad) / yGain; // unpad bbox bry to original

                    xMin = Clamp(xMin, 0, w - 0); // clip bbox tlx to boundaries
                    yMin = Clamp(yMin, 0, h - 0); // clip bbox tly to boundaries
                    xMax = Clamp(xMax, 0, w - 1); // clip bbox brx to boundaries
                    yMax = Clamp(yMax, 0, h - 1); // clip bbox bry to boundaries

                    PredictionBox.Label label = env.labels[k];

                    var prediction = new PredictionBox()
                    {
                        LabelName = label.name,
                        score = confidence,
                        rectangle = PredictionBox.RectangleF(xMin, yMin, xMax - xMin, yMax - yMin),
                        index = i,
                    };

                    result.Add(prediction);
                });
            });

            return result.ToList();
        }

        /// <summary>
        /// Removes overlapped duplicates (nms).
        /// </summary>
        public static List<PredictionBox> Suppress(DDShellEnv env, List<PredictionBox> items)
        {
            var result = new List<PredictionBox>(items);

            foreach (var item in items) // iterate every prediction
            {
                //var currents = result.ToList().Where(current => current == item);
                foreach (var current in result.ToList().Where(current => current != item)) // make a copy for each iteration
                {
                    var (rect1, rect2) = (item.rectangle, current.rectangle);

                    var intersection = PredictionBox.Intersect(rect1, rect2);

                    var intArea = intersection.Area(); // intersection area
                    var unionArea = rect1.Area() + rect2.Area() - intArea; // union area
                    var overlap = intArea / unionArea; // overlap ratio

                    if (overlap >= env.Overlap)
                    {
                        if (item.score >= current.score)
                        {
                            result.Remove(current);
                        }
                    }
                }
            }

            return result;
        }

        public static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
    }
}