using ConsoleApp1;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Yolov
{
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
    public class PredictionBox
    {
        public class Label
        {
            public int Id;
            public string Name;
        }

        public Label label;
        public RectangleF Rectangle;
        public float Score;
        public int Index;
    }
    public class YoloModel5
    {
        internal float Overlap { set => _model.Overlap = value; }

        public int Width { set => _model.Width = value; }
        public int Height { set => _model.Height = value; }


        protected InferenceSession _inferenceSession;

        public string OutputColumnName { get; set; }

        protected string[] modelOutputs;
        protected int ModelOutputDimensions;

        public float MulConfidence;
        public float ModelConfidence;
        public float ModelInputWidth { set => Width = (int)value; get => (float)_model.Width; }
        public float ModelInputHeight { set => Height = (int)value; get => (float)_model.Height; }

        struct Config
        {
            internal int Height;
            internal int Width;
            internal float Overlap;
        }
        Config _model;
        public Dictionary<int, string> labelDict;
        protected List<PredictionBox.Label> labels;
        public int ModelClassesCount { get => labelDict.Count; }


        public List<PredictionBox> Detect(string img_path, string seg_model_path)
        {
            ReadModel(seg_model_path);

            var image = SixLabors.ImageSharp.Image.Load<Rgba32>(img_path);
            var clone = image.Clone();
            int w = image.Width;
            int h = image.Height;
            var outputs = Inference(clone);
            var output0 = outputs[0];
            //var output1 = outputs[1];
            var raw = ParseDetect(outputs[0], w, h);
            var boxes = Suppress(raw);

            //int xPadding;
            //int yPadding;

            //var keepOriginalAspectRatio = true;
            //if (keepOriginalAspectRatio)
            //{
            //    var reductionRatio = Math.Min(_model.Width / (float)w, _model.Height / (float)h);

            //    xPadding = (int)((_model.Width - w * reductionRatio) / 2);
            //    yPadding = (int)((_model.Height - h * reductionRatio) / 2);
            //}
            //else
            //{
            //    xPadding = 0;
            //    yPadding = 0;
            //}

            //var classesCount = labelDict.Count;
            //var maskChannelCount = output0.Dimensions[1] - 4 - classesCount;
            //boxes.ForEach(box =>
            //{
            //    var maskWeights = GetMaskWeights(output0, box.Index, maskChannelCount, classesCount + 4);

            //    //var mask = ProcessMask(output1, maskWeights, box.Bounds, originSize, metadata.ImageSize, xPadding, yPadding);

            //    //var value = new SegmentationBoundingBox(box.Class, box.Bounds, box.Confidence, mask);

            //    //return value;
            //});

            return boxes;
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


        private List<DenseTensor<float>> Inference(Image<Rgba32> image)
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

        /// <summary>
        /// Removes overlapped duplicates (nms).
        /// </summary>
        private List<PredictionBox> Suppress(List<PredictionBox> items)
        {
            var result = new List<PredictionBox>(items);

            foreach (var item in items) // iterate every prediction
            {
                //var currents = result.ToList().Where(current => current == item);
                foreach (var current in result.ToList().Where(current => current!= item)) // make a copy for each iteration
                {
                    var (rect1, rect2) = (item.Rectangle, current.Rectangle);

                    var intersection = RectangleF.Intersect(rect1, rect2);

                    var intArea = intersection.Area(); // intersection area
                    var unionArea = rect1.Area() + rect2.Area() - intArea; // union area
                    var overlap = intArea / unionArea; // overlap ratio

                    if (overlap >= _model.Overlap)
                    {
                        if (item.Score >= current.Score)
                        {
                            result.Remove(current);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns value clamped to the inclusive range of min and max.
        /// </summary>
        public float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        // {box_x, box_y, box_w, box_h, confidence, class 1, ..., class 80, mask 1, mask2, ..., mask 32}
        virtual protected List<PredictionBox> ParseDetect(DenseTensor<float> output, int w, int h)
        {
            var result = new ConcurrentBag<PredictionBox>();

            var (xGain, yGain) = (ModelInputWidth / (float)w, ModelInputHeight / (float)h); // x, y gains
            var (xPad, yPad) = ((ModelInputWidth - w * xGain) / 2, (ModelInputHeight - h * yGain) / 2); // left, right pads

            Parallel.For(0, (int)output.Length / ModelOutputDimensions, (i) =>
            {
                if (output[0, i, 4] <= ModelConfidence) return; // skip low obj_conf results

                Parallel.For(5, ModelClassesCount, (j) =>
                {
                    output[0, i, j] = output[0, i, j] * output[0, i, 4]; // mul_conf = obj_conf * cls_conf
                });

                //Console.WriteLine($"{output[0, i, 0]} {output[0, i, 1]} {output[0, i, 2]} {output[0, i, 3]} {output[0, i, 4]}");

                Parallel.For(5, ModelClassesCount, (k) =>
                {
                    if (output[0, i, k] <= MulConfidence) return; // skip low mul_conf results

                    float xMin = ((output[0, i, 0] - output[0, i, 2] / 2) - xPad) / xGain; // unpad bbox tlx to original
                    float yMin = ((output[0, i, 1] - output[0, i, 3] / 2) - yPad) / yGain; // unpad bbox tly to original
                    float xMax = ((output[0, i, 0] + output[0, i, 2] / 2) - xPad) / xGain; // unpad bbox brx to original
                    float yMax = ((output[0, i, 1] + output[0, i, 3] / 2) - yPad) / yGain; // unpad bbox bry to original

                    xMin = Clamp(xMin, 0, w - 0); // clip bbox tlx to boundaries
                    yMin = Clamp(yMin, 0, h - 0); // clip bbox tly to boundaries
                    xMax = Clamp(xMax, 0, w - 1); // clip bbox brx to boundaries
                    yMax = Clamp(yMax, 0, h - 1); // clip bbox bry to boundaries

                    PredictionBox.Label label = labels[k - 5];

                    var prediction = new PredictionBox()
                    {
                        label = label,
                        Score = output[0, i, k],
                        Rectangle = new SixLabors.ImageSharp.RectangleF(xMin, yMin, xMax - xMin, yMax - yMin),
                        Index = i,
                    };

                    result.Add(prediction);
                });
            });

            return result.ToList();
        }

        virtual protected void ReadModel(string seg_model_path)
        {
            _inferenceSession = new InferenceSession(File.ReadAllBytes(seg_model_path));

            var keys = _inferenceSession.ModelMetadata.CustomMetadataMap.Keys.ToList();
            var names = _inferenceSession.ModelMetadata.CustomMetadataMap["names"];
            labelDict = ParseNames(names);
            labels = labelDict.ToList().ConvertAll(p =>new PredictionBox.Label { Id = p.Key, Name = p.Value});

            var modelInputs = _inferenceSession.InputMetadata.Keys.ToList();
            ModelInputWidth = _inferenceSession.InputMetadata[modelInputs[0]].Dimensions[2];
            ModelInputHeight = _inferenceSession.InputMetadata[modelInputs[0]].Dimensions[3];

            OutputColumnName = _inferenceSession.OutputMetadata.Keys.First();
            modelOutputs = _inferenceSession.OutputMetadata.Keys.ToArray();
            ModelOutputDimensions = _inferenceSession.OutputMetadata[modelOutputs[0]].Dimensions[2]; // [1,25200,39]
        }

        protected Dictionary<int, string> ParseNames(string names)
        {
            var dict = new Dictionary<int, string>();
            try
            {
                var arr = JArray.Parse(names);
                foreach (var name in arr)
                {
                    dict.Add(dict.Count, name.Value<string>());
                }
                return dict;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }

            try
            {
                var obj = JObject.Parse(names);
                foreach (var idx in obj.Properties())
                {
                    dict.Add(int.Parse(idx.Name), idx.Value.ToString());
                }
                return dict;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }

            return dict;
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
    }
}
