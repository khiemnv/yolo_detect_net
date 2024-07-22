using Yolov;

namespace annotation
{
    public class AnntShellEnv
    {
        public string yoloModelPath;
        public YoloModelBase yolo = null;
        public YoloC yoloC = null;
        public Traverser _traverser = new Traverser();
        public LabelConfig labelConfig = new annotation.LabelConfig();
        public ColorPalette colorPalette = new ColorPalette();
        public BaseConfig cfg;
        public WorkingDir _wkDir = new WorkingDir();
    }
}
