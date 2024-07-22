using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Yolov
{
    public class YoloC : YoloModelBase
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
        public List<Classification> RunClassification(string imgPath)
        {
            var img = Image.Load<Rgba32>(imgPath);
            return RunClassification(img);
        }

        public List<Classification> RunClassification(Image<Rgba32> img)
        {
            var raw = Inference(img);
            var i = 0;
            var ret = new List<Classification>();
            raw[0].ToList().ForEach(f =>
            {
                if (f > ModelConfidence)
                {
                    ret.Add(new Classification
                    {
                        Confidence = f,
                        Label = labelDict[i]
                    });
                }
                i++;
            });

            // order by desc
            ret.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
            return ret;
        }
    }
}
