using Models;
using Repositories;
using System.Diagnostics;

namespace Services
{
    public class UploadService : IUploadService
    {
        protected readonly DetectModelRepository detectModelRepository = new DetectModelRepository();
        protected readonly BarcodeCounterRepository barcodeCounterRepository = new BarcodeCounterRepository();
        protected readonly NotificationRepository notificationRepository = new NotificationRepository();
        protected readonly UserRepository userRepository = new UserRepository();
        protected readonly PanelRepository panelRepository = new PanelRepository();
        protected readonly PartRepository partRepository = new PartRepository();
        protected readonly QuarterTrimRepository quarterTrimRepository = new QuarterTrimRepository();
        protected readonly FileRepository fileRepository = new FileRepository();

        protected class UploadServiceCfg
        {
            public string root;
        }
        protected readonly UploadServiceCfg cfg = new UploadServiceCfg();
        public UploadService(string root)
        {
            cfg.root = root;
            _baseCfg = DataClient.GetBaseConfig();
        }


        protected bool stopFlag;
        protected Thread uploadThread;
        protected object myLock = new object();
        private bool scriptLoaded = false;

        Func<object, DisconnectedEventArgs, object> _OnDisconnected;
        Func<object, DisconnectedEventArgs, object> IUploadService.OnDisconnected { get => _OnDisconnected; set => _OnDisconnected = value; }
        Func<object, EventArgs, object> _OnRestart;
        Func<object, EventArgs, object> IUploadService.OnRestart { get => _OnRestart; set => _OnRestart = value; }
        public bool ScriptLoaded { get => scriptLoaded; set => scriptLoaded = value; }

        protected readonly DataClient.BaseConfig _baseCfg;

        protected virtual Thread CreateUploadThread()
        {
            Init();
            lock (myLock)
            {
                stopFlag = false;
            }
            var t = new Thread((p) =>
            {
                var disconnected = 0;
                var idx = 0;
                var retries = _baseCfg.UploadConfig.Retries;
                bool sendOK = false;
                var restartTime = _baseCfg.RestartTime;
                var autoRestart = _baseCfg.AutoRestart;
                var (startTime, endTime) = autoRestart ? ParseRestartTime(restartTime) : (-1, -1); // minutes
                var restartSw = Stopwatch.StartNew();

                while (true)
                {
                    try
                    {
                        // restart every day
                        if (startTime != -1)
                        {
                            var now = DateTime.Now;
                            var m = (now.Hour * 60 + now.Minute);
                            if (m >= startTime
                            && m < endTime
                            && restartSw.Elapsed.TotalMinutes > (endTime - startTime))
                            {
                                Logger.Debug("restart");
                                _OnRestart?.Invoke(this, new EventArgs());
                                restartSw.Restart();
                            }
                        }

                        // check interupt
                        lock (myLock)
                        {
                            if (stopFlag)
                            {
                                break;
                            }
                        }

                        sendOK = Work();

                        if (ScriptLoaded)
                        {
                            disconnected = NotifyStatus(disconnected, sendOK ? 0 : 1);
                        }

                        if (!sendOK)
                        {
                            idx++;
                            idx = Math.Min(retries.Count, idx);
                            if (idx > 0)
                            {
                                //Logger.Debug($"Retry after {retries[idx - 1]} seconds");
                                Thread.Sleep(retries[idx - 1] * 1000);
                            }
                        }
                        else
                        {
                            idx = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.Message);
                    }
                    Thread.Sleep(10 * 1000); // milisecond
                }
            });
            return t;
        }

        // if error return (-1,-1)
        private (int startTime, int endTime) ParseRestartTime(string restartTime)
        {
            int ParseHHMM(string hhmm)
            {
                var arr = hhmm.Split(':');
                return int.Parse(arr[0]) * 60 + int.Parse(arr[1]);
            }

            try
            {
                var arr = restartTime.Split('~');
                int startTime = ParseHHMM(arr[0]);
                int endTime = ParseHHMM(arr[1]);
                Debug.Assert(startTime <= endTime, $"startTime [{startTime}] should smaller than endTime [{endTime}]");
                return (startTime, endTime);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return (-1, -1);
            }
        }

        protected virtual void Init()
        {
            // init repository

            var db = Path.Combine(cfg.root, "DB");
            if (!Directory.Exists(db))
            {
                Directory.CreateDirectory(db);
            }

            var b = new bool[9];
            b[0] = detectModelRepository.LoadData(Path.Combine(cfg.root, "DB", "models.json"));
            b[1] = userRepository.LoadData(Path.Combine(cfg.root, "DB", "users.json"));
            b[2] = userRepository.Init(Path.Combine(cfg.root, "DB", "Users"));
            b[3] = barcodeCounterRepository.Init(Path.Combine(cfg.root, "DB", "BarcodeCounters"));
            b[4] = notificationRepository.Init(Path.Combine(cfg.root, "DB", "Notifications"));
            b[5] = panelRepository.Init(Path.Combine(cfg.root, "DB", "Panels"));
            b[6] = partRepository.Init(Path.Combine(cfg.root, "DB", "Parts"));
            b[7] = quarterTrimRepository.Init(Path.Combine(cfg.root, "DB", "QuarterTrims"));
            b[8] = fileRepository.Init(Path.Combine(cfg.root, "DB", "FileEntitys"));
            if (b.Any(v => v != true))
            {
                throw new Exception($"init repository fail! [{b.ToJson()}]");
            }
        }

        protected virtual bool Work()
        {
            bool sendOK = false;
            do
            {
                // checked time 12:00 ~ 13:00 && errors not empty

                // upload others

                var b0 = userRepository.Fetch(); // login & update token (in appconfig.json)
                if (!b0) { break; }

                // NOTE: if subscribe disable, push is no effect
                var b1 = barcodeCounterRepository.Push();
                if (!b1) { break; }

                // NOTE: in upload_sync_mode, push is no effect
                //var b3 = fileRepository.Push();
                //if (!b3) { break; }

                // NOTE: if subscribe disable, push is no effect
                //var b2 = quarterTrimRepository.Push();
                //if (!b2) { break; }

                //var b4 = panelRepository.Push();
                //var b5 = partRepository.Push();
                // NOTE: in upload_sync_mode, push image base64
                var b6 = notificationRepository.Push();
                if (!b6) { break; }

                sendOK = true;
            } while (false);
            return sendOK;
        }

        protected int NotifyStatus(int disconnected, int ret)
        {
            try
            {
                if (ret != disconnected)
                {
                    _OnDisconnected?.Invoke(this, new DisconnectedEventArgs() { IsConnected = ret });
                    disconnected = ret;
                }

                return disconnected;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return disconnected;
            }
        }

        public bool IsAlive()
        {
            return (uploadThread != null && uploadThread.IsAlive);
        }
        public void StopAndWait()
        {
            if (uploadThread != null && uploadThread.IsAlive)
            {
                lock (myLock)
                {
                    stopFlag = true;
                }
                uploadThread.Join();
            }
            DataClient.FinalClient();
        }

        public void Start()
        {
            DataClient.InitClient();
            uploadThread = CreateUploadThread();
            uploadThread.Start();
        }

        public virtual bool Add(BarcodeCounter bc)
        {
            return barcodeCounterRepository.Add(bc);
        }
        public virtual bool InfoScaned(BarcodeCounter bc)
        {
            return notificationRepository.InfoAdded(bc);
        }

        public DetectModel GetDetectModelByBarCode(string barcode)
        {
            return detectModelRepository.GetDetectModelByBarCode(barcode);
        }

        public bool Login(User user)
        {
            return userRepository.Login(user);
        }

        public virtual bool UploadFile(FileEntity f)
        {
            return fileRepository.UploadFile(f);
        }

        public virtual bool Add(QuarterTrim qt1)
        {
            return quarterTrimRepository.Add(qt1);
            //return true;
        }

        public virtual bool Add(Models.Panel panel)
        {
            //return panelRepository.Add(panel);
            return true;
        }

        public virtual bool Add(Part part)
        {
            //return partRepository.Add(p);
            return true;
        }

        public virtual bool InfoCreated(QuarterTrim qt1)
        {
            return notificationRepository.InfoAdded(qt1);
        }
    }
}
