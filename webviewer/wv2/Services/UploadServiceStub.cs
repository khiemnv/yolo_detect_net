using Models;
using Repositories;
using Panel = Models.Panel;

namespace Services
{
    public class UploadServiceStub : IUploadService
    {
        public UploadServiceStub(string root)
        {
            detectModelRepository.LoadData(Path.Combine(root, "DB", "models.json"));
        }
        protected readonly DetectModelRepository detectModelRepository = new DetectModelRepository();
        public Func<object, DisconnectedEventArgs, object> OnDisconnected { get; set; }
        public bool ScriptLoaded { get; set; }
        public Func<object, EventArgs, object> OnRestart { get; set; }

        public bool Add(BarcodeCounter bc)
        {
            return true;
        }

        public bool Add(QuarterTrim qt1)
        {
            return true;
        }

        public bool Add(Part part)
        {
            return true;
        }

        public bool Add(Panel panel)
        {
            return true;
        }

        public DetectModel GetDetectModelByBarCode(string barcode)
        {
            return detectModelRepository.GetDetectModelByBarCode(barcode);
        }

        public bool InfoCreated(QuarterTrim qt1)
        {
            return true;
        }

        public bool InfoScaned(BarcodeCounter bc)
        {
            return true;
        }

        public bool Login(User user)
        {
            return true;
        }

        public void Start()
        {
            DataClient.InitClient();
        }

        public void StopAndWait()
        {
        }

        public bool UploadFile(FileEntity f)
        {
            return true;
        }

        public bool IsAlive()
        {
            return false;
        }
    }
}
