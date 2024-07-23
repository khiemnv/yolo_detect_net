using Models;
using Repositories;
using System.Diagnostics;
using WP_GUI;
using Panel = Models.Panel;

namespace Services
{
    public class UploadService2 : UploadService
    {
        private readonly string _dbPath;
        private Stopwatch st;
        private Stopwatch st2;

        public UploadService2(string root) : base(root)
        {
            _dbPath = System.IO.Path.Combine(root, "DB");
            if (!Directory.Exists(_dbPath))
            {
                Directory.CreateDirectory(_dbPath);
            }
        }

        private bool Add(List<PostReq> list, string name)
        {
            try
            {
                string date = ProgramHelpers.GetTodayDateString();
                var path = $@"{_dbPath}\{date}\{name}.json";
                var json = list.ToJson();
                ProgramHelpers.CreateJsonFile(path, json);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return false;
            }
        }

        public override bool Add(BarcodeCounter bc)
        {
            var lst = new List<PostReq>
            {
                new PostReq
                {
                    ObjType = nameof(BarcodeCounter),
                    JsonData = bc.ToJson(),
                }
            };
            return Add(lst, $"{bc.Id}_bc");
        }

        public override bool Add(QuarterTrim qt)
        {
            qt = qt.ToJson().FromJson<QuarterTrim>(); // clone new obj
            var lst = new List<PostReq>();
            foreach (var panel in qt.Panels)
            {
                lst.Add(new PostReq
                {
                    ObjType = nameof(FileEntity),
                    JsonData = panel.BeforeImg.ToJson(),
                });

                if (_baseCfg.UploadConfig.UploadRes)
                {
                    lst.Add(new PostReq
                    {
                        ObjType = nameof(FileEntity),
                        JsonData = panel.ResultImg.ToJson(),
                    });
                }
                else
                {
                    panel.ResultImgId = null;
                }

                // edit panels
                panel.ResultImg = null;
                panel.BeforeImg = null;
            }

            lst.Add(new PostReq
            {
                ObjType = nameof(QuarterTrim),
                JsonData = qt.ToJson(),
            });

            // edit qt
            qt.Panels = null;
            lst.Add(new PostReq
            {
                ObjType = nameof(Notification),
                JsonData = NotificationRepository.CreateNotification(qt).ToJson(),
            });
            return Add(lst, $"{qt.Id}_qt");
        }

        public override bool Add(Part part)
        {
            return true;
        }

        public override bool Add(Panel panel)
        {
            return true;
        }

        public override bool UploadFile(FileEntity file)
        {
            return true;
        }

        public override bool InfoCreated(QuarterTrim qt1)
        {
            return true;
        }
        public override bool InfoScaned(BarcodeCounter bc)
        {
            return true;
        }

        protected override void Init()
        {
            // init repository
            var b = new[]
            {
                detectModelRepository.LoadData(Path.Combine(_dbPath, "models.json")),
                userRepository.LoadData(Path.Combine(_dbPath, "users.json")),
                userRepository.Init(Path.Combine(_dbPath, "Users")),
            };
            if (b.Any(v => v != true))
            {
                throw new Exception($"init repository fail! [{b.ToJson()}]");
            }

            st = Stopwatch.StartNew();
            st2 = Stopwatch.StartNew();
        }

        protected override bool Work()
        {
            bool sendOK;
            var ngTimeout = _baseCfg.UploadConfig.NgTimeout;
            var uploadInterval = _baseCfg.UploadConfig.UploadInterval;
            var updateTimeout = _baseCfg.UpdateConfig.Timeout;
            var enableUpdate = _baseCfg.UpdateConfig.Enable;

            // check connection
            if (DataClient.CheckConnection())
            {
                // upload others
                sendOK = Upload(uploadInterval);

                // retry every [ngTimeout] seconds
                if (st.Elapsed.TotalSeconds > ngTimeout)
                {
                    //Logger.Debug($"retry ng at {DateTimeOffset.Now:HH:mm:ss}");
                    st.Restart();
                    sendOK = RetryUpload(uploadInterval);
                }

                if (enableUpdate && st2.Elapsed.TotalSeconds > updateTimeout)
                {
                    // update config
                    Logger.Debug($"Update at {DateTimeOffset.Now:HH:mm:ss}");
                    st2.Restart();
                    Update();
                }
            }
            else
            {
                sendOK = false;
            }

            return sendOK;
        }

        protected virtual bool Update()
        {
            var cfg = DataClient.GetBaseConfig();
            var lastUpdate = cfg.UpdateConfig.LastUpdatedDate;
            var lst = CheckForUpdate(lastUpdate.AddSeconds(1));
            Logger.Debug($"CheckForUpdate({lastUpdate}), {lst.Count()} models modified");
            foreach (var m in lst)
            {
                UpdateConfig(m, cfg.WorkingDir);
                if (lastUpdate < m.ModifiedDate)
                {
                    lastUpdate = m.ModifiedDate.Value;
                }
            }

            if (lastUpdate > cfg.UpdateConfig.LastUpdatedDate)
            {
                cfg.UpdateConfig.LastUpdatedDate = lastUpdate;
                DataClient.WriteBaseConfig(cfg);
            }
            return true;
        }

        protected virtual bool Update2()
        {
            var cfg = DataClient.GetBaseConfig();
            var root = cfg.WorkingDir;
            var model = ProgramHelpers.CurBarcode;
            var sample_output = $"{root}\\model_{model}\\sample_{model}\\output";
            var cfgPath = $"{sample_output}\\{model}.config";
            var lastUpdate = cfg.UpdateConfig.LastUpdatedDate;
            if (File.Exists(cfgPath))
            {
                var qt = File.ReadAllText(cfgPath).FromJson<QuarterTrim>();
                lastUpdate = qt.CreatedDate.Value;
            }

            var lst = CheckForUpdate2(model, lastUpdate);
            foreach (var m in lst)
            {
                UpdateConfig(m, cfg.WorkingDir);
            }
            return true;
        }

        private bool UpdateConfig(DetectModel m, string root)
        {
            try
            {
                var model = m.PartNumber;
                var sample_output = $"{root}\\model_{model}\\sample_{model}\\output";
                var cfgPath = $"{sample_output}\\{model}.config";
                var b = DataClient.DownloadFile(cfgPath, m.ConfigFileId);
                if (!b)
                {
                    Logger.Error($"DownloadFile {m.ConfigFileId} error!");
                    return false;
                }

                var qt = File.ReadAllText(cfgPath).FromJson<QuarterTrim>();
                XmlExporter.CreateXmls(qt, sample_output);
                Logger.Debug($"{model} was updated!");
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                return false;
            }
        }

        private IEnumerable<DetectModel> CheckForUpdate2(string model, DateTimeOffset lastUpdate)
        {
            var m = DataClient.GetDetectModelByBarCode(model);
            if (m.ModifiedDate > lastUpdate)
            {
                return new List<DetectModel>() { m };
            }
            else
            {
                return Enumerable.Empty<DetectModel>();
            }
        }
        private IEnumerable<DetectModel> CheckForUpdate(DateTimeOffset lastUpdate)
        {
            return detectModelRepository.SelectModifiedModelsByDate(lastUpdate);
        }

        protected virtual bool RetryUpload(int uploadInterval)
        {
            // retry upload file & qrt
            Logger.Debug("retry upload file & qrt");
            var lst = GetRecentDataFolders(uploadInterval);
            foreach (var sub in lst)
            {
                var ngPath = Path.Combine(sub.FullName, "NG");
                if (!Directory.Exists(ngPath)) { continue; }

                foreach (var file in new DirectoryInfo(ngPath).GetFiles())
                {
                    bool b = SendFileAndQrt(file);
                    if (b == true)
                    {
                        // move to OK
                        var okPath = sub.CreateSubdirectory("OK");
                        file.MoveTo(Path.Combine(okPath.FullName, file.Name));
                    }
                }
            }

            return true;
        }
        protected virtual bool Upload(int uploadInterval)
        {
            var b0 = userRepository.Fetch(); // login & update token (in appconfig.json)
            if (!b0) { return false; }

            // upload file & qrt in [uploadInterval] days
            IEnumerable<DirectoryInfo> lst = GetRecentDataFolders(uploadInterval);
            foreach (var sub in lst)
            {
                foreach (var file in sub.GetFiles())
                {
                    bool b = SendFileAndQrt(file);
                    if (b == true)
                    {
                        // move to OK
                        var ngPath = sub.CreateSubdirectory("OK");
                        file.MoveTo(Path.Combine(ngPath.FullName, file.Name));
                        //file.Delete();
                    }
                    else
                    {
                        // move to NG
                        var ngPath = sub.CreateSubdirectory("NG");
                        file.MoveTo(Path.Combine(ngPath.FullName, file.Name));
                    }
                }
            }

            return true;
        }

        private IEnumerable<DirectoryInfo> GetRecentDataFolders(int uploadInterval)
        {
            List<string> days = GetAvaiableDays(new DateTimeOffset(DateTime.Now), uploadInterval);
            var lst = new DirectoryInfo(_dbPath).GetDirectories()
                .Where(sub => days.Contains(sub.Name));
            return lst;
        }

        public static List<string> GetAvaiableDays(DateTimeOffset cur, int n)
        {
            var days = new List<string>();
            for (double i = 0; i > -n; i--)
            {
                days.Add(cur.AddDays(i).ToString("yyyyMMdd"));
            }

            return days;
        }

        // sequence
        // + notify start
        // + upload files,
        // + save qrt,
        // + notify result
        private bool SendFileAndQrt(FileInfo file)
        {
            var json = System.IO.File.ReadAllText(file.FullName);
            var lst = json.FromJson<List<PostReq>>();
            var b = true;
            foreach (var req in lst)
            {
                if (req.StatusCode == "OK") { continue; }
                switch (req.ObjType)
                {
                    case nameof(BarcodeCounter):
                        b = barcodeCounterRepository.Post(req.JsonData.FromJson<BarcodeCounter>());
                        break;
                    case nameof(FileEntity):
                        b = fileRepository.Post(req.JsonData.FromJson<FileEntity>());
                        break;
                    case nameof(QuarterTrim):
                        var qrt = req.JsonData.FromJson<QuarterTrim>();
                        // remove file obj before post
                        foreach (var p in qrt.Panels)
                        {
                            p.ResultImg = null;
                            p.BeforeImg = null;
                        }
                        b = quarterTrimRepository.Post(qrt);
                        req.JsonData = qrt.ToJson(); // update QuarterTrim.No
                        break;
                    case nameof(Notification):
                        //var n = req.JsonData.FromJson<Notification>();
                        var qt = lst.Find(x => x.ObjType == nameof(QuarterTrim)).JsonData.FromJson<QuarterTrim>();
                        var n = NotificationRepository.CreateNotification(qt);
                        b = notificationRepository.Post(n);
                        break;
                }
                req.StatusCode = b ? "OK" : "NG";
                if (b == false) { break; }
            }

            System.IO.File.WriteAllText(file.FullName, lst.ToJson());
            return b;
        }
    }
}
