namespace Yolov
{
    public interface IYoloModel
    {
        PredictionBoxCollection Detect(string img_path, string model_path);
    }
}