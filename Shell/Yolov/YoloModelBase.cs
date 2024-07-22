using annotation;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Yolov
{
    internal static class ImageContoursHelper
    {
        private static readonly (Func<Point, Point> func, int neighborhood)[] _neighborhood;

        static ImageContoursHelper()
        {
            _neighborhood = new (Func<Point, Point>, int)[]
            {
            (point => new Point(point.X - 1, point.Y), 7),
            (point => new Point(point.X - 1, point.Y - 1), 7),
            (point => new Point(point.X, point.Y - 1), 1),
            (point => new Point(point.X + 1, point.Y - 1), 1),
            (point => new Point(point.X + 1, point.Y), 3),
            (point => new Point(point.X + 1, point.Y + 1), 3),
            (point => new Point(point.X, point.Y+1), 5),
            (point => new Point(point.X -1, point.Y + 1), 5)
            };
        }

        public static IReadOnlyList<IReadOnlyList<Point>> FindContours(this Image image)
        {
            var luminance = image.CloneAs<L8>();

            var found = new HashSet<Point>();

            bool inside = false;

            var contours = new List<IReadOnlyList<Point>>();

            for (int y = 0; y < luminance.Height; y++)
                for (int x = 0; x < luminance.Width; x++)
                {
                    Point point = new Point(x, y);

                    if (found.Contains(point) && !inside)
                    {
                        inside = true;
                        continue;
                    }

                    bool transparent = IsTransparent(luminance, point);

                    if (!transparent && inside)
                        continue;

                    if (transparent && inside)
                    {
                        inside = false;
                        continue;
                    }

                    if (!transparent && !inside)
                    {
                        var contour = new List<Point>();

                        contours.Add(contour);

                        found.Add(point);
                        contour.Add(point);

                        int checkLocationNr = 1;
                        Point startPos = point;

                        int counter1 = 0;
                        int counter2 = 0;

                        while (true)
                        {
                            Point checkPosition = _neighborhood[checkLocationNr - 1].func(point);

                            int newCheckLocationNr = _neighborhood[checkLocationNr - 1].neighborhood;

                            if (!IsTransparent(luminance, checkPosition))
                            {
                                if (checkPosition == startPos)
                                {
                                    counter1++;

                                    if (newCheckLocationNr == 1 || counter1 >= 3)
                                    {
                                        inside = true;
                                        break;
                                    }
                                }

                                checkLocationNr = newCheckLocationNr;
                                point = checkPosition;
                                counter2 = 0;
                                found.Add(point);
                                contour.Add(point);
                            }
                            else
                            {
                                checkLocationNr = 1 + (checkLocationNr % 8);

                                if (counter2 > 8)
                                    break;
                                else
                                    counter2++;
                            }
                        }
                    }
                }

            return contours;
        }

        private static bool IsTransparent(Image<L8> image, Point pixel)
        {
            return pixel.X > image.Width - 1
                   || pixel.X < 0
                   || pixel.Y > image.Height - 1
                   || pixel.Y < 0
                   || image[pixel.X, pixel.Y].PackedValue == 0;
        }
    }
    public interface IMask
    {
        float this[int x, int y] { get; }

        int Width { get; }

        int Height { get; }

        float GetConfidence(int x, int y);
    }

    public class Mask : IMask
    {
        public Mask(float[,] xy)
        {
            _xy = xy;
        }
        private readonly float[,] _xy;
        public float this[int x, int y] => _xy[x, y];

        public int Width { get => _xy.GetLength(0); }

        public int Height { get => _xy.GetLength(1); }

        //float IMask.this[int x, int y] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public float GetConfidence(int x, int y)
        {
            return _xy[x, y];
        }
    }
    public class SegmentationBoundingBox
    {
        public string name;
        public Rectangle bounds;
        public float confidence;
        public IMask mask;
        public int id;
        public (String Name, int Id) label { get => (name, id); }
        public System.Drawing.Rectangle Rectangle { get => new System.Drawing.Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height); }
        public double angle;
        public double area;
    }
    public enum LabelKind
    {
        Generic,
        InstanceSeg,
    }
    public static class RectangleExtensions
    {
        /// <summary>
        /// Calculates rectangle area.
        /// </summary>
        public static float Area(this SixLabors.ImageSharp.RectangleF source)
        {
            return source.Width * source.Height;
        }
    }

    internal struct Config
    {
        internal int Height;
        internal int Width;
        internal float Overlap;
    }
    public class YoloModelBase
    {
        public Dictionary<int, string> labelDict;
        public float ModelConfidence;

        public float MulConfidence;


        protected InferenceSession _inferenceSession;
        protected List<PredictionBox.Label> labels;
        protected int ModelOutputDimensions;

        protected string[] modelOutputs;
        Config _model;
        internal string DeviceDetect;
        public int Height { set => _model.Height = value; }
        public int ModelClassesCount { get => labelDict.Count; }
        public float ModelInputHeight { set => Height = (int)value; get => (float)_model.Height; }
        public float ModelInputWidth { set => Width = (int)value; get => (float)_model.Width; }

        public string OutputColumnName { get; set; }

        public int Width { set => _model.Width = value; }
        public float Overlap { set => _model.Overlap = value; }

        public static float GetConfidence(byte luminance)
        {
            return (luminance - 255) * -1 / 255F;
        }

        public static byte GetLuminance(float confidence)
        {
            return (byte)((confidence * 255 - 255) * -1);
        }

        // {box_x, box_y, box_w, box_h, confidence, class 1, ..., class 80, mask 1, mask2, ..., mask 32}
        private static ReadOnlySpan<float> GetMaskWeights(Tensor<float> output, int boxIndex, int maskChannelCount, int maskWeightsOffset)
        {
            var maskWeights = new float[maskChannelCount];

            for (int i = 0; i < maskChannelCount; i++)
            {
                maskWeights[i] = output[0, maskWeightsOffset + i, boxIndex];
            }

            return maskWeights;
        }
        public static float Sigmoid(float value)
        {
            var k = Math.Exp(value);
            return (float)(k / (1.0f + k));
        }

        /// <summary>
        /// Returns value clamped to the inclusive range of min and max.
        /// </summary>
        public static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        virtual public PredictionBoxCollection Detect(string img_path, string model_path)
        {
            if (!string.IsNullOrEmpty(model_path))
            {
                ReadModel(model_path);
            }
            if (!string.IsNullOrEmpty(img_path))
            {
                return Detect(img_path);
            }
            return null;
        }
#if UNSAFE
        public virtual PredictionBoxCollection DetectUnsafe(string imgPath)
        {
            var env = this;
            var (ts, imgSize) = DDShell.ExtractPixelsUnsafe(env, imgPath);
            var output = DDShell.Inference(env, ts);
            var raw = DDShell.ParseDetect(env, output[0], imgSize.Width, imgSize.Height);
            var boxes = DDShell.Suppress(env, raw);
            return new PredictionBoxCollection { w = imgSize.Width, h = imgSize.Height, boxes = boxes };
        }
#endif
        virtual public PredictionBoxCollection Detect(string img_path)
        {
            var image = SixLabors.ImageSharp.Image.Load<Rgba32>(img_path);
            return Detect(image);
        }

        public PredictionBoxCollection Detect(Image<Rgba32> image)
        {
            int w = image.Width;
            int h = image.Height;

            var outputs = Inference(image);
            var raw = ParseDetect(outputs[0], w, h);
            var boxes = Suppress(raw);

            return new PredictionBoxCollection { w = w, h = h, boxes = boxes };
        }

        public List<SegmentationBoundingBox> SegDetect(string img_path, string model_path)
        {
            if (!string.IsNullOrEmpty(model_path))
                ReadModel(model_path);

            if (string.IsNullOrEmpty(img_path)) return null;

            List<SegmentationBoundingBox> segs = SegDetect(img_path);

            return segs;
        }

        virtual protected List<SegmentationBoundingBox> SegDetect(string img_path)
        {
            var image = Image.Load<Rgba32>(img_path);
            //var clone = image.Clone();
            int w = image.Width;
            int h = image.Height;
            var outputs = Inference(image);

            // { box_x, box_y, box_w, box_h, confidence, class 1, ..., class 80, mask 1, mask2, ..., mask 32}
            var output0 = outputs[0]; // [1,38,8400] classes.count = 2
            var output1 = outputs[1]; // [1,32,160,160]
            var raw = ParseDetect(outputs[0], w, h);
            List<PredictionBox> boxes = Suppress(raw);

            int xPadding;
            int yPadding;

            var keepOriginalAspectRatio = false;
            if (keepOriginalAspectRatio)
            {
                var reductionRatio = Math.Min(_model.Width / (float)w, _model.Height / (float)h);

                xPadding = (int)((_model.Width - w * reductionRatio) / 2);
                yPadding = (int)((_model.Height - h * reductionRatio) / 2);
            }
            else
            {
                xPadding = 0;
                yPadding = 0;
            }

            var maskChannelCount = ModelOutputDimensions - 4 - ModelClassesCount; // 32
            var segs = boxes.ConvertAll(box =>
            {
                // ...
                // index: 0~(8400-1) | box_x, box_y, box_w, box_h, confidence, class 1, ..., class n, mask 1, mask2, ..., mask 32
                // ...                 [0]                         [4]                                [4+n]
                var maskWeights = GetMaskWeights(output0, box.index, maskChannelCount, ModelClassesCount + 4); // float[32]

                var bounds = new Rectangle((int)box.rectangle.X, (int)box.rectangle.Y, (int)box.rectangle.Width, (int)box.rectangle.Height);
                var mask = ProcessMask(output1, maskWeights,
                    bounds,
                    new Size(w, h), // org size
                    new Size(_model.Width, _model.Height), // modelSize 640x640 
                    xPadding, yPadding);

                var value = new SegmentationBoundingBox()
                {
                    name = box.label.name,
                    id = box.label.id,
                    bounds = bounds,
                    confidence = box.score,
                    mask = mask
                };

                return value;
            });
            return segs;
        }

        // {box_x, box_y, box_w, box_h, confidence, class 1, ..., class 80, mask 1, mask2, ..., mask 32}
        virtual protected List<PredictionBox> ParseDetect(DenseTensor<float> output, int w, int h)
        {
            throw new NotImplementedException();
        }



        virtual protected void ReadModel(string model_path)
        {
            _inferenceSession = CreateSection(DeviceDetect, model_path);

            var keys = _inferenceSession.ModelMetadata.CustomMetadataMap.Keys.ToList();
            var names = _inferenceSession.ModelMetadata.CustomMetadataMap["names"];
            labelDict = ParseNames(names);
            labels = labelDict.ToList().ConvertAll(p => new PredictionBox.Label { id = p.Key, name = p.Value });

            var modelInputs = _inferenceSession.InputMetadata.Keys.ToList();
            ModelInputWidth = _inferenceSession.InputMetadata[modelInputs[0]].Dimensions[2];
            ModelInputHeight = _inferenceSession.InputMetadata[modelInputs[0]].Dimensions[3];

            OutputColumnName = _inferenceSession.OutputMetadata.Keys.First();
            modelOutputs = _inferenceSession.OutputMetadata.Keys.ToArray();
            ModelOutputDimensions = _inferenceSession.OutputMetadata[modelOutputs[0]].Dimensions[2]; // [1,25200,39]
        }

        private Tensor<float> ExtractPixels(Image<Rgba32> image)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, _model.Height, _model.Width });

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


        virtual protected List<DenseTensor<float>> Inference(Image<Rgba32> image)
        {
            if (image.Width != _model.Width || image.Height != _model.Height)
            {
                image.Mutate(x => x.Resize(_model.Width, _model.Height)); // fit image size to specified input size
            }
            var tensor = ExtractPixels(image);

            var inputs = new List<NamedOnnxValue> // add image as input
            {
                NamedOnnxValue.CreateFromTensor("images", tensor)
            };
            var result = _inferenceSession.Run(inputs);
            var raw = result.Select(i => i.Value as DenseTensor<float>).ToList();
            return raw;
        }

        private Mask ProcessMask(DenseTensor<float> maskPrototypes,
            ReadOnlySpan<float> maskWeights, Rectangle bounds,
            Size originSize, Size modelSize, int xPadding, int yPadding)
        {
            // [1,32,160,160]
            var maskChannels = maskPrototypes.Dimensions[1];
            var maskHeight = maskPrototypes.Dimensions[2];
            var maskWidth = maskPrototypes.Dimensions[3];

            if (maskChannels != maskWeights.Length)
                throw new InvalidOperationException();

            var bitmap = new Image<L8>(maskWidth, maskHeight);

            for (int y = 0; y < maskHeight; y++)
            {
                for (int x = 0; x < maskWidth; x++)
                {
                    var value = 0F;

                    for (int i = 0; i < maskChannels; i++)
                        value += maskPrototypes[0, i, y, x] * maskWeights[i];

                    value = Sigmoid(value);

                    var color = GetLuminance(value);
                    var pixel = new L8(color);

                    bitmap[x, y] = pixel;
                }
            }

            var xPad = xPadding * maskWidth / modelSize.Width;
            var yPad = yPadding * maskHeight / modelSize.Height;

            var paddingCropRectangle = new SixLabors.ImageSharp.Rectangle(xPad,
                                                     yPad,
                                                     maskWidth - xPad * 2,
                                                     maskHeight - yPad * 2);

            var lines = new List<string>();
            for (int y = 0; y < 160; y++)
            {
                var line = "";
                for (int x = 0; x < 160; x++)
                {
                    line += ($"{bitmap[x, y]},");
                }
                lines.Add(line);
            }
            System.IO.File.WriteAllLines("bitmap.csv", lines);


            lines = new List<string>();
            for (int y = 0; y < 160; y++)
            {
                var line = "";
                for (int x = 0; x < 160; x++)
                {
                    line += ($"{maskPrototypes[0, 0, y, x]},");
                }
                lines.Add(line);
            }
            System.IO.File.WriteAllLines("maskPrototypes0.csv", lines);

            bitmap.Mutate(x =>
            {
                // crop for preprocess resize padding
                x.Crop(paddingCropRectangle);

                // resize to original image size
                x.Resize(originSize);

                // crop for getting the object segmentation only
                x.Crop(bounds);
            });
            //bitmap.SaveAsJpeg("bitmap.jpeg");

            var final = new float[bounds.Width, bounds.Height];

            bitmap.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        ref var pixel = ref pixelRow[x];
                        var confidence = GetConfidence(pixel.PackedValue);
                        final[x, y] = confidence;
                    }
                }
            });

            return new Mask(final);
        }

        /// <summary>
        /// Removes overlapped duplicates (nms).
        /// </summary>
        protected List<PredictionBox> SuppressOld(List<PredictionBox> items)
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

                    if (overlap >= _model.Overlap)
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
        protected List<PredictionBox> Suppress(List<PredictionBox> items)
        {
            var result = new List<PredictionBox>();

            foreach (var item in items) // iterate every prediction
            {
                var found = result.FindIndex(max =>
                {
                    var (rect1, rect2) = (item.rectangle, max.rectangle);

                    var intersection = PredictionBox.Intersect(rect1, rect2);

                    var intArea = intersection.Area(); // intersection area
                    var unionArea = rect1.Area() + rect2.Area() - intArea; // union area
                    var overlap = intArea / unionArea; // overlap ratio

                    if (overlap >= _model.Overlap)
                    {
                        if (item.score >= max.score)
                        {
                            return true;
                        }
                    }
                    return false;
                });
                if (found != -1)
                {
                    result[found] = item;
                }
                else
                { result.Add(item); }
            }

            return result;
        }

        public static Dictionary<int, string> ParseNames(string names)
        {
            var dict = new Dictionary<int, string>();
            try
            {
                if (Regex.IsMatch(names, @"^\["))
                {
                    var arr = JArray.Parse(names);
                    foreach (var name in arr)
                    {
                        dict.Add(dict.Count, name.Value<string>());
                    }
                    return dict;
                }

                var obj = JObject.Parse(names);
                foreach (var idx in obj.Properties())
                {
                    dict.Add(int.Parse(idx.Name), idx.Value.ToString());
                }
                return dict;
            }
            catch (Exception ex)
            {
                Logger.Logger.Error(ex.Message);
                throw;
            }
        }
        protected static InferenceSession CreateSection(string device, string model_path)
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
        public virtual void Dispose()
        {
            _inferenceSession?.Dispose();
        }
    }
}