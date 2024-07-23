using Microsoft.ML.OnnxRuntime;

namespace Yolov
{
    public class DDShellEnv
    {
        internal InferenceSession _inferenceSession;

        public int Width { get; set; }

        public int Height { get; set; }
        public int ModelOutputDimensions { get; internal set; }
        public int ModelInputWidth { get; internal set; }
        public int ModelInputHeight { get; internal set; }
        public int ModelClassesCount { get; internal set; }
        public float Overlap { get; set; } = 0.45f;

        public Dictionary<int, string> labelDict;
        public float ModelConfidence = 0.5f;
        public string DeviceDetect;
        public List<PredictionBox.Label> labels;
    }
}