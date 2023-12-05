
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Yolov
{
    public class YoloModel8: YoloModel5
    {
        protected override void ReadModel(string seg_model_path)
        {
            base.ReadModel(seg_model_path);
            ModelOutputDimensions = _inferenceSession.OutputMetadata[modelOutputs[0]].Dimensions[1]; // [1,38,8400]
        }

        protected override List<PredictionBox> ParseDetect(DenseTensor<float> output, int w, int h)
        {
            var result = new ConcurrentBag<PredictionBox>();

            var (xGain, yGain) = (ModelInputWidth / (float)w, ModelInputHeight / (float)h); // x, y gains
            var (xPad, yPad) = ((ModelInputWidth - w * xGain) / 2, (ModelInputHeight - h * yGain) / 2); // left, right pads

            // [1,38,8400]
            // i=0~8400
            Parallel.For(0, (int)output.Length / ModelOutputDimensions, (i) =>
            {

                //Console.WriteLine($"{output[0, i, 0]} {output[0, i, 1]} {output[0, i, 2]} {output[0, i, 3]} {output[0, i, 4]}");
                // j=0~1
                Parallel.For(0, ModelClassesCount, (k) =>
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

                    PredictionBox.Label label = labels[k];

                    var prediction = new PredictionBox()
                    {
                        label = label,
                        Score = confidence,
                        Rectangle = new SixLabors.ImageSharp.RectangleF(xMin, yMin, xMax - xMin, yMax - yMin),
                        Index = i,
                    };

                    result.Add(prediction);
                });
            });

            return result.ToList();
        }
    }
}
