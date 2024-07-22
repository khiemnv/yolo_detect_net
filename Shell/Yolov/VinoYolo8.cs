using OpenCvSharp;
using OpenCvSharp.Dnn;
using OpenVinoSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Yolov
{
    public class VinoYolo8 : YoloModelBase
    {
        CompiledModel compiled_model;
        InferRequest infer_request;
        float[] input_data;
        OpenVinoSharp.Tensor input_tensor;
        Shape input_shape;
        OpenVinoSharp.Tensor output_tensor;
        Shape output_shape;
        int output_length;
        private Core core;
        private Model model;

        private Dictionary<int, string> ReadLabelDict(string path)
        {
            var dict = new Dictionary<int, string>();
            var lines = System.IO.File.ReadAllLines(path);
            var s = 0;
            lines.ToList().ForEach(line =>
            {
                switch (s)
                {
                    case 0:
                        if (System.Text.RegularExpressions.Regex.IsMatch(line, "names:"))
                        {
                            s = 1;
                        }
                        break;
                    case 1:
                        var arr = line.Split(new char[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                        dict.Add(int.Parse(arr[0]), arr[1]);
                        break;
                }
            });
            return dict;
        }

        // file.xml or .onnx
        protected override void ReadModel(string xml)
        {
            core = new Core();
            model = core.read_model(xml);
            if (DeviceDetect == "multi")
            {
                var dict = new Dictionary<string, string>() {
                    { "MULTI_DEVICE_PRIORITIES", "GPU,CPU" }
                };
                compiled_model = core.compile_model(model, "MULTI", dict);
            }
            else
            {
                compiled_model = core.compile_model(model, "AUTO");
            }

            infer_request = compiled_model.create_infer_request();
            input_tensor = infer_request.get_tensor("images");
            input_shape = input_tensor.get_shape();
            input_data = new float[input_shape[1] * input_shape[2] * input_shape[3]];

            output_tensor = infer_request.get_tensor("output0");
            output_shape = output_tensor.get_shape();
            output_length = (int)output_tensor.get_size();

            // metadata.yaml or dataset.yaml or names.json
            var parent = System.IO.Path.GetDirectoryName(xml);
            var yaml = System.IO.Directory.GetFiles(parent, "*.yaml").FirstOrDefault();
            var namesjson = System.IO.Path.Combine(parent, "names.json");
            if (yaml != null)
            {
                labelDict = ReadLabelDict(yaml);
            }
            else if (System.IO.File.Exists(namesjson))
            {
                var json = System.IO.File.ReadAllText(namesjson);
                labelDict = ParseNames(json);
            }
            else
            {
                throw new Exception("missing label info");
            }

            labels = labelDict.ToList().ConvertAll(p => new PredictionBox.Label { id = p.Key, name = p.Value });

            ModelInputHeight = input_shape[2];
            ModelInputWidth = input_shape[3];
        }
        public override void Dispose()
        {
            infer_request?.Dispose();
            compiled_model?.Dispose();
            model?.Dispose();
            core?.Dispose();
        }
#if UNSAFE
        public override PredictionBoxCollection DetectUnsafe(string image_path)
        {
            throw new NotImplementedException();
        }
#endif
        public override PredictionBoxCollection Detect(string image_path)
        {
            Mat image = new Mat(image_path); // Read image by opencvsharp
            int max_image_length = image.Cols > image.Rows ? image.Cols : image.Rows;
            Mat max_image = Mat.Zeros(new OpenCvSharp.Size(max_image_length, max_image_length), MatType.CV_8UC3);
            Rect roi = new Rect(0, 0, image.Cols, image.Rows);
            image.CopyTo(new Mat(max_image, roi));

            float factor = (float)max_image_length / (float)Math.Max(ModelInputHeight, ModelInputWidth);
            Mat input_mat = CvDnn.BlobFromImage(max_image, 1.0 / 255.0, new OpenCvSharp.Size(input_shape[2], input_shape[3]), new Scalar(0), true, false);
            Marshal.Copy(input_mat.Ptr(0), input_data, 0, input_data.Length);

            input_tensor.set_data<float>(input_data);
            infer_request.infer();
            var (pbc, _) = ParseOutput2((image.Width, image.Height), factor);
            return pbc;
        }

        private PredictionBoxCollection ParseOutput((int Width, int Height) image, float factor)
        {
            float[] output_data = output_tensor.get_data<float>(output_length);
            Mat result_data = Mat.FromPixelData((int)output_shape[1], (int)output_shape[2], MatType.CV_32F, output_data);
            result_data = result_data.T();

            // Storage results list
            List<Rect> position_boxes = new List<Rect>();
            List<int> class_ids = new List<int>();
            List<int> row_ids = new List<int>();
            List<float> confidences = new List<float>();
            // Preprocessing output results
            for (int i = 0; i < result_data.Rows; i++)
            {
                Mat classes_scores = new Mat(result_data, new Rect(4, i, ModelClassesCount, 1));
                OpenCvSharp.Point max_classId_point, min_classId_point;
                double max_score, min_score;
                // Obtain the maximum value and its position in a set of data
                Cv2.MinMaxLoc(classes_scores, out min_score, out max_score,
                    out min_classId_point, out max_classId_point);
                // Confidence level between 0 ~ 1
                // Obtain identification box information
                if (max_score > ModelConfidence)
                {
                    float cx = result_data.At<float>(i, 0);
                    float cy = result_data.At<float>(i, 1);
                    float ow = result_data.At<float>(i, 2);
                    float oh = result_data.At<float>(i, 3);
                    int x = (int)((cx - 0.5 * ow) * factor);
                    int y = (int)((cy - 0.5 * oh) * factor);
                    int width = (int)(ow * factor);
                    int height = (int)(oh * factor);
                    Rect box = new Rect();
                    box.X = Math.Max(0, x);
                    box.Y = Math.Max(0, y);
                    box.Width = Math.Min(width, image.Width - box.X);
                    box.Height = Math.Min(height, image.Height - box.Y);

                    position_boxes.Add(box);
                    class_ids.Add(max_classId_point.X);
                    confidences.Add((float)max_score);
                    row_ids.Add(i);
                }
            }
            // NMS non maximum suppression
            int[] indexes = new int[position_boxes.Count];
            CvDnn.NMSBoxes(position_boxes, confidences, 0.5f, 0.5f, out indexes);

            var boxes = new List<PredictionBox>();
            for (int i = 0; i < indexes.Length; i++)
            {
                int index = indexes[i];
                int id = class_ids[index];
                var pb = position_boxes[index];
                var box = new PredictionBox(id, labelDict[id], Math.Max(0, pb.X), Math.Max(0, pb.Y), pb.Width, pb.Height);
                box.score = confidences[index];
                box.index = row_ids[index];
                boxes.Add(box);
            }

            return new PredictionBoxCollection { w = image.Width, h = image.Height, boxes = boxes };
        }


        private (PredictionBoxCollection, Mat) ParseOutput2((int Width, int Height) image, float factor)
        {
            var result = new ConcurrentBag<PredictionBox>();
            float[] output_data = output_tensor.get_data<float>(output_length);
            Mat result_data = Mat.FromPixelData((int)output_shape[1], (int)output_shape[2], MatType.CV_32F, output_data);

            // Preprocessing output results
            Parallel.For(0, (int)output_shape[2], (int i) =>
            {
                Parallel.For(0, ModelClassesCount, (int k) =>
                {
                    var confidence = result_data.At<float>(k + 4, i);
                    if (confidence > ModelConfidence)
                    {
                        float cx = result_data.At<float>(0, i);
                        float cy = result_data.At<float>(1, i);
                        float ow = result_data.At<float>(2, i);
                        float oh = result_data.At<float>(3, i);
                        float x = (float)((cx - 0.5 * ow) * factor);
                        float y = (float)((cy - 0.5 * oh) * factor);
                        float width = (ow * factor);
                        float height = (oh * factor);

                        x = Clamp(x, 0, image.Width - 0); // clip bbox tlx to boundaries
                        y = Clamp(y, 0, image.Height - 0); // clip bbox tly to boundaries
                        width = Clamp(width, 0, image.Width - x - 1); // clip bbox brx to boundaries
                        height = Clamp(height, 0, image.Height - y - 1); // clip bbox bry to boundaries

                        PredictionBox.Label label = labels[k];

                        var prediction = new PredictionBox()
                        {
                            label = label,
                            score = confidence,
                            rectangle = PredictionBox.RectangleF(x, y, width, height),
                            index = i,
                        };

                        result.Add(prediction);
                    }
                });
            });

            var boxes = Suppress(result.ToList());
            return (new PredictionBoxCollection { w = image.Width, h = image.Height, boxes = boxes }, result_data);
        }

        protected override List<SegmentationBoundingBox> SegDetect(string image_path)
        {
            Mat image = new Mat(image_path); // Read image by opencvsharp
            int max_image_length = image.Cols > image.Rows ? image.Cols : image.Rows;
            Mat max_image = Mat.Zeros(new OpenCvSharp.Size(max_image_length, max_image_length), MatType.CV_8UC3);
            Rect roi = new Rect(0, 0, image.Cols, image.Rows);
            image.CopyTo(new Mat(max_image, roi));

            float factor = (float)max_image_length / Math.Max(ModelInputHeight, ModelInputWidth);
            Mat input_mat = CvDnn.BlobFromImage(max_image, 1.0 / 255.0, new OpenCvSharp.Size(input_shape[2], input_shape[3]), new Scalar(0), true, false);
            Marshal.Copy(input_mat.Ptr(0), input_data, 0, input_data.Length);

            input_tensor.set_data<float>(input_data);
            infer_request.infer();
            return ParseSegOutput((image.Width, image.Height), factor);
        }
        private static ReadOnlySpan<float> GetMaskWeights(Mat output0, int boxIndex, int maskChannelCount, int maskWeightsOffset)
        {
            var maskWeights = new float[maskChannelCount];

            for (int i = 0; i < maskChannelCount; i++)
            {
                maskWeights[i] = output0.At<float>(maskWeightsOffset + i, boxIndex);
            }

            return maskWeights;
        }
        private List<SegmentationBoundingBox> ParseSegOutput((int Width, int Height) image, float factor)
        {
            var (pbc, output0) = ParseOutput2(image, factor);

            var output_tensor1 = infer_request.get_tensor("output1");
            var output_shape2 = output_tensor1.get_shape();
            var output_length2 = (int)output_tensor1.get_size();
            float[] output_data2 = output_tensor1.get_data<float>(output_length2);
            var sizes = new[] { (int)output_shape2[1], (int)output_shape2[2], (int)output_shape2[3] };
            Mat result_data2 = Mat.FromPixelData(sizes, MatType.CV_32F, output_data2);

            var segs = new List<SegmentationBoundingBox>();
            var maskChannelCount = (int)output_shape[1] - 4 - ModelClassesCount;
            Debug.Assert(output_shape2[1] == maskChannelCount);

            var maskChannels = (int)output_shape2[1];
            var maskHeight = (int)output_shape2[2];
            var maskWidth = (int)output_shape2[3];

            int xPadding = 0;
            int yPadding = 0;
            var originSize = new SixLabors.ImageSharp.Size(image.Width, image.Height);
            var modelSize = new SixLabors.ImageSharp.Size((int)ModelInputWidth, (int)ModelInputHeight);

            //{

            //    var lines = new List<string>();
            //    for (int y = 0; y < 160; y++)
            //    {
            //        var line = "";
            //        for (int x = 0; x < 160; x++)
            //        {
            //            line += ($"{result_data2.At<float>(0, y, x)},");
            //        }
            //        lines.Add(line);
            //    }
            //    System.IO.File.WriteAllLines("result_data20.csv", lines);
            //}

            foreach (var box in pbc.boxes)
            {
                var maskWeights = GetMaskWeights(output0, box.index, maskChannelCount, 4 + ModelClassesCount);
                Rectangle bounds;
                bounds = new SixLabors.ImageSharp.Rectangle((int)box.rectangle.X, (int)box.rectangle.Y, (int)box.rectangle.Width, (int)box.rectangle.Height);
                var mask = ProcessMask2(result_data2, maskChannels, maskHeight, maskWidth, xPadding, yPadding, originSize, modelSize, box, maskWeights, bounds);

                segs.Add(new SegmentationBoundingBox()
                {
                    name = box.label.name,
                    id = box.label.id,
                    bounds = bounds,
                    confidence = box.score,
                    mask = mask
                });
            }

            return segs;
        }

        private static Mask ProcessMask(Mat output2, int maskChannels, int maskHeight, int maskWidth, int xPadding, int yPadding, SixLabors.ImageSharp.Size originSize, SixLabors.ImageSharp.Size modelSize, PredictionBox box, ReadOnlySpan<float> maskWeights, Rectangle bounds)
        {
            var bitmap = new SixLabors.ImageSharp.Image<L8>(maskWidth, maskHeight);
            for (int y = 0; y < maskHeight; y++)
            {
                for (int x = 0; x < maskWidth; x++)
                {
                    var value = 0F;

                    for (int i = 0; i < maskChannels; i++)
                        value += output2.At<float>(i, y, x) * maskWeights[i];

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

            //var lines = new List<string>();
            //for (int y = 0; y < 160; y++)
            //{
            //    var line = "";
            //    for (int x = 0; x < 160; x++)
            //    {
            //        line += ($"{bitmap[x, y]},");
            //    }
            //    lines.Add(line);
            //}
            //System.IO.File.WriteAllLines("bitmap_vino.csv", lines);

            bitmap.Mutate(x =>
            {
                // crop for preprocess resize padding
                x.Crop(paddingCropRectangle);

                // resize to original image size
                x.Resize(originSize.Width, originSize.Width);

                // crop for getting the object segmentation only
                x.Crop(bounds);
            });
            //bitmap.SaveAsJpeg("bitmap_vino.jpeg");

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
            var mask = new Mask(final);
            return mask;
        }

        private static IMask ProcessMask2(Mat output2, int maskChannels, int maskHeight, int maskWidth, int xPadding, int yPadding, SixLabors.ImageSharp.Size originSize, SixLabors.ImageSharp.Size modelSize, PredictionBox box, ReadOnlySpan<float> maskWeights, Rectangle bounds)
        {
            var bitmap = new VinoMask(maskWidth, maskHeight);
            for (int y = 0; y < maskHeight; y++)
            {
                for (int x = 0; x < maskWidth; x++)
                {
                    var value = 0F;

                    for (int i = 0; i < maskChannels; i++)
                        value += output2.At<float>(i, y, x) * maskWeights[i];

                    value = Sigmoid(value);

                    var color = GetLuminance(value);
                    //var pixel = new L8(color);

                    bitmap.mask.At<Vec3b>(y, x)[0] = color;
                }
            }

            var xPad = xPadding * maskWidth / modelSize.Width;
            var yPad = yPadding * maskHeight / modelSize.Height;

            var paddingCropRectangle = new SixLabors.ImageSharp.Rectangle(xPad,
                                                     yPad,
                                                     maskWidth - xPad * 2,
                                                     maskHeight - yPad * 2);

            //var lines = new List<string>();
            //for (int y = 0; y < 160; y++)
            //{
            //    var line = "";
            //    for (int x = 0; x < 160; x++)
            //    {
            //        line += ($"{bitmap[x, y]},");
            //    }
            //    lines.Add(line);
            //}
            //System.IO.File.WriteAllLines("bitmap_vino.csv", lines);

            Cv2.Resize(bitmap.mask, bitmap.mask, new OpenCvSharp.Size(originSize.Width, originSize.Width));
            // crop for getting the object segmentation only
            bitmap.mask = bitmap.mask[bounds.Y, bounds.Bottom, bounds.X, bounds.Right];

            bitmap.mask.SaveImage("bitmap_vino.jpeg");

            return bitmap;
        }
    }
}
