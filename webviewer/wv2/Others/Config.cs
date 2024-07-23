namespace WP_GUI
{
    public class Config
    {
        public string m_excelFile;
        public string m_sheetName = "config";
        public string m_model = "7230B086XA";//"7230B313XA";
        public string m_modelType = "yolov8x";//yolov8x, faster_rcnn_resnet152
        public float m_ModelConfidence = 0.6f;
        public string detect_method = "objdetect";// classify or objdetect 
        public string ModelDir => $"{root}\\model_{m_model}";

        public string SampleDir => $"{root}\\model_{m_model}\\sample_{m_model}";
        public string SampleOutDir => $@"{this.SampleDir}\output";

        public string CaptureDir => $"{root}\\model_{m_model}\\capture_{m_model}";

        public string CaptureImage => $"{root}\\data\\CaptureImage";

        public string Report => $"{root}\\data\\Report";

        public string SampleDir_lib => $"{lib}\\lib\\model_{m_model}\\sample_{m_model}";

        public string Detector => $"{lib}\\lib\\net6.0-windows";

        public string Html_Render => $"{lib}\\lib\\Html_Render";

        public string Firefox => $"{lib}\\lib\\Firefox64";

        public string FileReport => $"{lib}\\lib\\Report\\Template.xlsx";

        public string FileReportYear => $"{lib}\\lib\\Report\\Report.xlsx";

        public string root = "data";

        public string autoLogin = "";

        public string lib = Path.GetFullPath(Environment.CurrentDirectory);

        public string detector_debug = "detector_debug";
        internal string m_deviceDetect;

        //public string captureInDir = "in";

        //public string captureOutDir = "out";
        //internal string ModelFile => $@"{this.SampleDir}\output\model_{m_model}.onnx";
        internal string CropModelFile => $@"{this.SampleDir}\output\model_{m_model}_crop.onnx";

        //public string CreateCaptureDir()
        //{
        //    var capture_input = Path.Combine(CaptureDir, "input");
        //    _ = Directory.CreateDirectory(capture_input);
        //    //var n = (dir.GetFiles().Count + 1).ToString();
        //    //var newName = Path.Combine(capture_input, $"{m_selectedModel}_{n}_ci1.jpeg");
        //    //return newName;
        //    return "";
        //}
    }
}
