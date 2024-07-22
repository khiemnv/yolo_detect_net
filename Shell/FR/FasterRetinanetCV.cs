using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yolov;

namespace FR
{
    public class FasterRetinanetCV : FasterRetinanet
    {
        public override PredictionBoxCollection Detect(string image_path)
        {

            Mat image = new Mat(image_path); // Read image by opencvsharp
            int w = image.Width;
            int h = image.Height;
            var tensor = ExtractPixels(image);
            var outputs = Inference(tensor);
            var raw = ParseDetect(outputs, w, h);
            var boxes = Suppress(raw);

            return new PredictionBoxCollection { w = w, h = h, boxes = boxes };
        }
        private Tensor<byte> ExtractPixels(Mat image)
        {
            var tensor = new DenseTensor<byte>(new[] { 1, image.Height, image.Width, 3 });

            Parallel.For(0, image.Height, y =>
            {
                Parallel.For(0, image.Width, x =>
                {
                    tensor[0, y, x, 0] = image.At<Vec3b>(y, x)[0]; // r
                    tensor[0, y, x, 1] = image.At<Vec3b>(y, x)[1]; // g
                    tensor[0, y, x, 2] = image.At<Vec3b>(y, x)[2]; // b
                });
            });

            return tensor;
        }

        List<DenseTensor<float>> Inference(Tensor<byte> tensor)
        {
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

            return raw;
        }
        protected List<PredictionBox> ParseDetect(List<DenseTensor<float>> outputs, int w, int h)
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
                    rectangle = PredictionBox.RectangleF(xMin, yMin, xMax - xMin, yMax - yMin),
                    index = i,
                };

                result.Add(prediction);
            });

            return result.ToList();
        }

    }
}
