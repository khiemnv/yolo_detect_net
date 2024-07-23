using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Concurrent;

namespace Yolov
{

    public class YoloModel5 : YoloModelBase
    {
        override protected List<PredictionBox> ParseDetect(DenseTensor<float> output, int w, int h)
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

                //Logger.Logger.Debug($"{output[0, i, 0]} {output[0, i, 1]} {output[0, i, 2]} {output[0, i, 3]} {output[0, i, 4]}");

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
                        LabelName = label.name,
                        score = output[0, i, k],
                        rectangle = PredictionBox.RectangleF(xMin, yMin, xMax - xMin, yMax - yMin),
                        index = i, // 0~(8400-1)
                    };

                    result.Add(prediction);
                });
            });

            return result.ToList();
        }
    }
}
