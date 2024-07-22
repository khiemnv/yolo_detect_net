using System.Text.RegularExpressions;

namespace annotation
{
    public class WorkingDir
    {
        public string path;
        public string YOLODataset
        {
            get
            {
                return $"{path}\\YOLODataset";
            }
            set
            {
                if (!Regex.IsMatch(value, @"\\YOLODataset$")) { throw new System.Exception(); }
                path = Regex.Replace(value, @"\\YOLODataset$", "");
            }
        }

        public string models => $"{path}\\models";
        //public string test => $"{path}\\test";
        public string test
        {
            get
            {
                if (BaseConfig.GetBaseConfig().CompareWith == "test")
                {
                    return $"{path}\\test";
                }
                return $"{path}\\YOLODataset\\images";
            }
        }

        public string names_txt => $"{path}\\names.txt";
        public string dataset_yaml => $"{path}\\YOLODataset\\dataset.yaml";
        public string bak => $"{path}\\.bak";
        public string classifyDataset => $"{path}\\classifyDataset";
        public string classifyModel => $"{path}\\models\\yolov8x_cls\\best.onnx";

        public string ModelName { get => Regex.Match(path, "model_(\\w+)").Groups[1].Value; }
    }
}
