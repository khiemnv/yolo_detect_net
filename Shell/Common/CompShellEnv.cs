using comp;
using Yolov;

namespace annotation
{
    public class CompShellEnv
    {
        public WorkingDir _wkDir = new WorkingDir();
        public CompModels _cmp = new CompModels();
        public Traverser _traverser = new Traverser();
        public YoloModelBase yolo = null;
        public string yoloModelPath;
        public string _deviceDetect;
    }
}
