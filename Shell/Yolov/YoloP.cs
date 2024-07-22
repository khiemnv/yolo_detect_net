using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Yolov
{
    public class YoloP : YoloModelBase
    {
        public class Classification
        {
            /// <summary>
            /// Label of classified image.
            /// </summary>
            public string Label { get; set; }

            /// <summary>
            /// Confidence score of classified image.
            /// </summary>
            public double Confidence { get; set; }
        }
        protected override void ReadModel(string path)
        {
            _inferenceSession = new InferenceSession(File.ReadAllBytes(path));
            var names = _inferenceSession.ModelMetadata.CustomMetadataMap["names"];
            labelDict = ParseNames(names);
            labels = labelDict.ToList().ConvertAll(p => new PredictionBox.Label { id = p.Key, name = p.Value });

            var modelInputs = _inferenceSession.InputMetadata.Keys.ToList();
            ModelInputWidth = _inferenceSession.InputMetadata[modelInputs[0]].Dimensions[2];
            ModelInputHeight = _inferenceSession.InputMetadata[modelInputs[0]].Dimensions[3];
        }
        public List<PredictionBox> RunClassification(string imgPath)
        {
            var img = Image.Load<Rgba32>(imgPath);
            return RunClassification(img);
        }

        public List<PredictionBox> RunClassification(Image<Rgba32> img)
        {
            var result = new ConcurrentBag<PredictionBox>();
            var (w, h) = (img.Width, img.Height);

            var (xGain, yGain) = (ModelInputWidth / (float)w, ModelInputHeight / (float)h); // x, y gains
            var (xPad, yPad) = ((ModelInputWidth - w * xGain) / 2, (ModelInputHeight - h * yGain) / 2); // left, right pads

            var raw = Inference(img);
            var output = raw[0];

            var points = 16;
            var dim = 5 + 16 * 2;
            var classes = 1;
            Parallel.For(0, (int)output.Length / dim, (i) =>
            {
                Parallel.For(0, classes, (k) =>
                {
                    // ...      [4,     5]
                    // x,y,w,h, class0, class1
                    var confidence = output[0, k + 4, i];
                    if (confidence <= ModelConfidence) return; // skip low obj_conf results
                    float xMin = ((output[0, 0, i] - output[0, 2, i] / 2) - xPad) / xGain; // unpad bbox tlx to original
                    float yMin = ((output[0, 1, i] - output[0, 3, i] / 2) - yPad) / yGain; // unpad bbox tly to original
                    float xMax = ((output[0, 0, i] + output[0, 2, i] / 2) - xPad) / xGain; // unpad bbox brx to original
                    float yMax = ((output[0, 1, i] + output[0, 3, i] / 2) - yPad) / yGain; // unpad bbox bry to original

                    xMin = Clamp(xMin, 0, w - 0); // clip bbox tlx to boundaries
                    yMin = Clamp(yMin, 0, h - 0); // clip bbox tly to boundaries
                    xMax = Clamp(xMax, 0, w - 1); // clip bbox brx to boundaries
                    yMax = Clamp(yMax, 0, h - 1); // clip bbox bry to boundaries

                    var poses = new List<PointF>();
                    for (var j = 0; j < points; j++)
                    {
                        poses.Add(new PointF((output[0, 5 + j * 2, i] - xPad) / xGain, (output[0, 5 + j * 2 + 1, i] - yPad) / yGain));
                    }

                    PredictionBox.Label label = labels[k];
                    var prediction = new PredictionBox()
                    {
                        label = label,
                        score = confidence,
                        rectangle = PredictionBox.RectangleF(xMin, yMin, xMax - xMin, yMax - yMin),
                        index = i,
                        poses = poses,
                    };

                    result.Add(prediction);
                });
            });


            var boxes = Suppress(result.ToList());

            return boxes;
        }
    }
}
