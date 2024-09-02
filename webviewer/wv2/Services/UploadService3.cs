using Models;
using Repositories;
using SQLiteWithEF;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using Panel = Models.Panel;

namespace Services
{
    public class PostReqRepo
    {
        private readonly string _dbPath;
        readonly DatabaseContext _context;
        SessionMaster _curSession;
        protected object _curSessionLock = new object();
        private List<FileEntity> _curSessionFiles;
        public PostReqRepo(string root)
        {
            _dbPath = System.IO.Path.Combine(root, "DB");
            if (!Directory.Exists(_dbPath))
            {
                Directory.CreateDirectory(_dbPath);
            }
            _context = new SQLiteWithEF.DatabaseContext();

            _context.Migrate();

            // clean old session
            var baseCfg = DataClient.GetBaseConfig();
            var interval = -baseCfg.cleanRule.FromNow;
            var cmd = $"DELETE FROM SessionMaster WHERE CreatedDate < '{DateTime.Now.AddDays(interval):u}'";
            _context.Database.ExecuteSqlCommand(cmd);
            _context.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, "vacuum");
        }
        public bool Add(List<PostReq> list, string _)
        {
            try
            {
                lock (_curSessionLock)
                {
                    var lst = _curSession.Data.FromJson<List<PostReq>>();
                    lst.AddRange(list);
                    _curSession.Data = lst.ToJson();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return false;
            }
        }

        public void InitSession(string id)
        {
            lock (_curSessionLock)
            {
                _curSessionFiles = new List<FileEntity>();
                _curSession = new SessionMaster
                {
                    Id = id,
                    Data = "[]",
                    CreatedDate = DateTime.Now,
                };
            }
        }

        public string FinishSection()
        {
            var id = _curSession.Id;
            lock (_curSessionLock)
            {
                _curSessionFiles = null;
                SaveSession(_curSession);
                _curSession = null;
            }
            return id;
        }

        public void UpdateSession(SessionMaster chg)
        {
            lock (_curSessionLock)
            {
                if (_curSession != null && chg.Id == _curSession.Id)
                {
                    PatchSessionReqs(_curSession, chg);
                    return;
                }
            }

            SaveSession(chg);
        }

        private void PatchSessionReqs(SessionMaster org, SessionMaster chg)
        {
            var patch = chg.Data.FromJson<List<PostReq>>();
            var lst = org.Data.FromJson<List<PostReq>>();
            Debug.Assert(lst.Count >= patch.Count);
            for (int i = 0; i < patch.Count; i++)
            {
                lst[i].StatusCode = patch[i].StatusCode;
            }
            org.Data = lst.ToJson();

            // update status
            if (chg.Status == (int)SessionMasterStatus.NG)
            {
                org.Status = (int)SessionMasterStatus.NG;
                org.Count++;
            }
            else
            {
                var last = lst.Last();
                if (last != null
                    && last.ObjType == nameof(Notification)
                    && last.StatusCode == "OK")
                {
                    org.Status = (int)SessionMasterStatus.OK;
                }
            }
        }

        private void SaveSession(SessionMaster chg)
        {
            var old = _context.SessionMaster.Find(chg.Id);
            if (old != null)
            {
                // patch: post req result
                PatchSessionReqs(old, chg);
                _context.SessionMaster.AddOrUpdate(old);
            }
            else
            {
                _context.SessionMaster.AddOrUpdate(chg);
            }
            _context.SaveChanges();
        }
        public IEnumerable<SessionMaster> GetLastSession(int uploadInterval)
        {
            var dt = DateTime.Now.AddDays(-uploadInterval);
            var lst = _context.SessionMaster
                .Where(sub => sub.Status != (int)SessionMasterStatus.OK
                && sub.CreatedDate > dt
                )
                .ToList();
            lock (_curSessionLock)
            {
                if (_curSession != null)
                {
                    lst.Add(new SessionMaster
                    {
                        Id = _curSession.Id,
                        Data = _curSession.Data,
                        CreatedDate = _curSession.CreatedDate,
                    });
                }
            }
            return lst;
        }

        public IEnumerable<SessionMaster> GetLastNGSection(int uploadInterval)
        {
            var dt = DateTime.Now.AddDays(-uploadInterval);
            return _context.SessionMaster
                .Where(sub => sub.Status != (int)SessionMasterStatus.OK && sub.CreatedDate > dt)
                .ToList();
        }

        public List<SessionMaster> GetSessionById(string id)
        {
            var lst = _context.SessionMaster
                .Where(s => s.Id == id && s.Status != (int)SessionMasterStatus.OK)
                .ToList();
            return lst;
        }
    }
    public class UploadService3 : UploadService2
    {
        PostReqRepo _postReqRepo;
        public UploadService3(string root) : base(root)
        {
            _postReqRepo = new PostReqRepo(root);
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
            _postReqRepo.InitSession(bc.Id);
            return _postReqRepo.Add(lst, $"{bc.Id}_bc");
        }

        public override bool Add(QuarterTrim qt)
        {
            qt = qt.ToJson().FromJson<QuarterTrim>(); // clone new obj
            var lst = new List<PostReq>();
            var i = 0;
            foreach (var panel in qt.Panels)
            {
                //lst.Add(new PostReq
                //{
                //    objType = nameof(FileEntity),
                //    jsonData = panel.BeforeImg.ToJson(),
                //});
                var beforeImg = panel.BeforeImg;
                Debug.Assert(beforeImg != null, $"beforeImg[{i}] was not added");
                Debug.Assert(Path.GetFileName(beforeImg.Path) == $"ci_{i + 1}.jpeg", $"beforeImg[{i}] invalid name");

                //panel.BeforeImgId = beforeImg.Id;
                //panel.ResultImgId = null;

                //if (_baseCfg.uploadConfig.uploadRes)
                //{
                //    lst.Add(new PostReq
                //    {
                //        objType = nameof(FileEntity),
                //        jsonData = panel.ResultImg.ToJson(),
                //    });
                //}
                //else
                //{
                //    panel.ResultImgId = null;
                //}

                // edit panels
                //panel.ResultImg = null;
                //panel.BeforeImg = null;
                i++;
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
            });
            _postReqRepo.Add(lst, $"{qt.Id}_qt");

            var id = _postReqRepo.FinishSection();

            // start new thread to upload result
            if (_baseCfg.UploadConfig.Upload1)
            {
                ThreadPool.QueueUserWorkItem((x) =>
                {
                    if (DataClient.CheckConnection())
                    {
                        Upload1((string)x);
                    }
                }, id);
            }

            return true;
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
            //var found = _curSessionFiles.Find(f => f.Path == file.Path);
            //if (found != null) { return true; }

            //_curSessionFiles.Add(file);
            var lst = new List<PostReq>
            {
                new PostReq
                {
                    ObjType = nameof(FileEntity),
                    JsonData = file.ToJson(),
                }
            };
            return _postReqRepo.Add(lst, $"{file.Id}_img");
        }

        public override bool InfoCreated(QuarterTrim qt1)
        {
            return true;
        }
        public override bool InfoScaned(BarcodeCounter bc)
        {
            return true;
        }

        protected override bool RetryUpload(int uploadInterval)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                // retry upload file & qrt
                //Logger.Debug("retry upload file & qrt");
                var lst = _postReqRepo.GetLastNGSection(uploadInterval);
                //Logger.Debug($"RetryUpload {lst.Count()}");
                foreach (var sub in lst)
                {
                    var reqLst = sub.Data.FromJson<List<PostReq>>();
                    bool b = PostReqs(reqLst);
                    Logger.Debug($"RetryUpload {sub.Id} {sub.CreatedDate:u} {b}");
                    var clone = new SessionMaster
                    {
                        Id = sub.Id,
                        Status = b ? (int)SessionMasterStatus.OK : (int)SessionMasterStatus.NG,
                        Data = reqLst.ToJson(),
                    };
                    _postReqRepo.UpdateSession(clone);
                }
            }
            finally
            {
                if (lockTaken)
                {
                    _spinLock.Exit();
                }
            }
            return true;
        }

        private SpinLock _spinLock = new SpinLock(true);
        protected override bool Upload(int uploadInterval)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);

                var b0 = userRepository.Fetch(); // login & update token (in appconfig.json)
                if (!b0) { return false; }

                // upload file & qrt in [uploadInterval] days
                var lst = _postReqRepo.GetLastSession(uploadInterval);
                //Logger.Debug($"Upload {lst.Count()}");
                foreach (var sub in lst)
                {
                    var reqLst = sub.Data.FromJson<List<PostReq>>();
                    var b = PostReqs(reqLst);
                    var clone = new SessionMaster
                    {
                        Id = sub.Id,
                        Status = b ? (int)SessionMasterStatus.OK : (int)SessionMasterStatus.NG,
                        Data = reqLst.ToJson(),
                    };
                    _postReqRepo.UpdateSession(clone);
                }
            }
            finally
            {
                if (lockTaken)
                {
                    _spinLock.Exit();
                }
            }
            return true;
        }
        protected bool Upload1(string id)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);

                // upload file & qrt in [uploadInterval] days
                var lst = _postReqRepo.GetSessionById(id);

                //Logger.Debug($"Upload1 {lst.Count}");
                foreach (var s in lst)
                {
                    var reqLst = s.Data.FromJson<List<PostReq>>();
                    var b = PostReqs(reqLst);
                    var clone = new SessionMaster
                    {
                        Id = s.Id,
                        Status = b ? (int)SessionMasterStatus.OK : (int)SessionMasterStatus.NG,
                        Data = reqLst.ToJson(),
                    };
                    _postReqRepo.UpdateSession(clone);
                }
            }
            finally
            {
                if (lockTaken)
                {
                    _spinLock.Exit();
                }
            }
            return true;
        }

        // sequence
        // upload files,
        // save qrt,
        // notify result
        private bool PostReqs(List<PostReq> lst)
        {
            var b = true;
            foreach (var req in lst)
            {
                if (req.StatusCode == "OK") { continue; }
                switch (req.ObjType)
                {
                    case nameof(BarcodeCounter):
                        var bc = req.JsonData.FromJson<BarcodeCounter>();
                        b = barcodeCounterRepository.Post(bc);
                        Logger.Debug($"Post {req.ObjType} {bc.Id} {b}");
                        break;
                    case nameof(FileEntity):
                        {
                            var f = req.JsonData.FromJson<FileEntity>();
                            var uploadDir = _baseCfg.UploadDir;
                            if (Directory.Exists(uploadDir))
                            {
                                b = true;
                            }
                            else
                            {
                                b = fileRepository.Post(f);
                                Logger.Debug($"Post {req.ObjType} {f.Id} {b}");
                            }
                        }
                        break;
                    case nameof(QuarterTrim):
                        {
                            var qrt = req.JsonData.FromJson<QuarterTrim>();
                            var uploadDir = _baseCfg.UploadDir;
                            foreach (var p in qrt.Panels)
                            {
                                p.ResultImg = null;
                                if (!Directory.Exists(uploadDir))
                                {
                                    // remove file obj before post
                                    p.BeforeImgId = p.BeforeImg.Id;
                                    p.BeforeImg = null;
                                }
                                else
                                {
                                    p.BeforeImgId = null;
                                    var sub = Directory.CreateDirectory(Path.Combine(uploadDir, DateTime.Now.ToString("yyyyMMdd")));
                                    var fi = new FileInfo(p.BeforeImg.Path);
                                    var newimg = fi.CopyTo(Path.Combine(sub.FullName, p.BeforeImg.Id + "_" + fi.Name));
                                    p.BeforeImg.Path = newimg.FullName;
                                }
                            }
                            b = quarterTrimRepository.Post(qrt);
                            if (b)
                            {
                                // if susscess, update QuarterTrim.No
                                req.JsonData = qrt.ToJson();
                            }
                            Logger.Debug($"Post {req.ObjType} {qrt.Id} {b}");
                        }
                        break;
                    case nameof(Notification):
                        var qt = lst.Find(x => x.ObjType == nameof(QuarterTrim)).JsonData.FromJson<QuarterTrim>();
                        var n = NotificationRepository.CreateNotification(qt);
                        req.JsonData = n.ToJson();
                        //var n = req.JsonData.FromJson<Notification>();
                        b = notificationRepository.Post(n);
                        Logger.Debug($"Post {req.ObjType} {n.Id} {b}");
                        break;
                }
                req.StatusCode = b ? "OK" : "NG";
                if (b == false) { break; }
            }

            return b;
        }
    }
}
