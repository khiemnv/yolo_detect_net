using Models;
using Panel = Models.Panel;

namespace Services
{
    public class DisconnectedEventArgs : EventArgs
    {
        public int IsConnected { get; set; }
    }
    public delegate void DisconnectedEventHandler(object sender, DisconnectedEventArgs e);
    public interface IUploadService
    {

        //public DisconnectedEventHandler OnDisconnected;
        Func<object, DisconnectedEventArgs, object> OnDisconnected { get; set; }
        Func<object, EventArgs, object> OnRestart { get; set; }

        bool ScriptLoaded { get; set; }

        bool Add(BarcodeCounter bc);
        bool Add(QuarterTrim qt1);
        bool Add(Part part);
        bool Add(Panel panel);
        DetectModel GetDetectModelByBarCode(string barcode);
        bool InfoCreated(QuarterTrim qt1);
        bool InfoScaned(BarcodeCounter bc);
        bool IsAlive();
        bool Login(User user);
        void Start();
        void StopAndWait();
        bool UploadFile(FileEntity f);
    }
}