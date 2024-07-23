using Models;
using Services;
using static Services.DataClient;
using Panel = Models.Panel;

namespace Repositories
{
    public class DetectModelRepository : TRepository<DetectModel>
    {
        public DetectModel GetDetectModelByBarCode(string barCode)
        {
            return entitys.Find(m => m.PartNumber == barCode);
        }

        internal IEnumerable<DetectModel> SelectModifiedModelsByDate(DateTimeOffset lastUpdate)
        {
            return DataClient.SelectModifiedModelsByDate(lastUpdate);
        }
    }

    public interface IRepository<TEntity>
        where TEntity : class, IEntity
    {
        bool Add(TEntity e);
        bool Post(TEntity entity);
    }
    public abstract class TRepository<TEntity> : IRepository<TEntity>
        where TEntity : class, IEntity
    {
        public string path = string.Empty;
        public List<TEntity> entitys;
        public bool Init(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            // if create dir fail
            if (Directory.Exists(path))
            {
                this.path = path;
                return true;
            }
            return false;
        }
        public bool LoadData(string jsonFile)
        {
            var ret = false;
            try
            {
                // download if not exist
                if (!File.Exists(jsonFile))
                {
                    var all = GetAll();
                    File.WriteAllText(jsonFile, all.ToJson());
                }

                string json = System.IO.File.ReadAllText(jsonFile);
                entitys = json.FromJson<List<TEntity>>();
                ret = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
            return ret;
        }

        public virtual IEnumerable<TEntity> GetAll() { return GetAll<TEntity>(); }

        public bool Add(TEntity e)
        {
            try
            {
                File.WriteAllText($@"{path}\{e.Id}.json", e.ToJson());
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"{e.GetType().Name} {e.Id} {ex.Message}");
                return false;
            }
        }
        public virtual bool Post(TEntity entity) { throw new NotImplementedException(); }
        public bool Push()
        {
            var ret = false;
            try
            {
                var di = new DirectoryInfo(path);
                foreach (var fi in di.GetFiles())
                {
                    string json = File.ReadAllText(fi.FullName);
                    TEntity e = json.FromJson<TEntity>();
                    bool ok = Post(e);
                    if (ok)
                    {
                        fi.Delete();
                    }
                    else
                    {
                        // skip error
                        Logger.Error($"post {e.GetType().Name} error {e.Id}");
                    }
                }
                ret = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
            return ret;
        }
    }
    public class BarcodeCounterRepository : TRepository<BarcodeCounter>
    {
        public override bool Post(BarcodeCounter entity)
        {
            return PostCreateBarcodeCounter(entity) != null;
        }
    }
    public class PanelRepository : TRepository<Panel>
    {
        public override bool Post(Panel entity)
        {
            return PostCreatePanel(entity) != null;
        }
    }
    public class PartRepository : TRepository<Part>
    {
        public override bool Post(Part entity)
        {
            return PostCreatePart(entity) != null;
        }
    }
    public class QuarterTrimRepository : TRepository<QuarterTrim>
    {
        public override bool Post(QuarterTrim entity)
        {
            if (entity == null) return false;

            var cfg = DataClient.GetBaseConfig();
            if (!cfg.UploadConfig.Subscribe)
            {
                // NOTE: if subscribe is false, do nothing
                return true;
            }
            var added = PostCreateQuarterTrim(entity);
            if (added != null)
            {
                // update QuarterTrim.No
                entity.No = added.No;
            }
            return added != null;
        }
    }
    public class FileRepository : TRepository<FileEntity>
    {
        public bool UploadFile(FileEntity f) => Add(f);
        public override bool Post(FileEntity entity)
        {
            if (entity == null) return false;

            var cfg = DataClient.GetBaseConfig();
            if (cfg.UploadConfig.UploadSync)
            {
                // NOTE: in upload_sync_mode, do nothing
                return true;
            }
            return UploadFileAsync(entity) != null;
        }
    }

    public class FileWithBase64 : FileEntity
    {
        public string Base64 { get; set; }
    }
    public class QuarterTrimWithFiles
    {
        public QuarterTrim QT { get; set; } = null;
        public List<FileWithBase64> Files { get; set; } = null;
    }

    public class NotificationRepository : TRepository<Notification>
    {
        public bool InfoAdded(QuarterTrim qt)
        {
            Notification data = CreateNotification(qt);
            return Add(data);
        }

        public static Notification CreateNotification(QuarterTrim qt)
        {
            var imgs = new List<FileEntity>();
            var cfg = DataClient.GetBaseConfig();
            var pl = new QuarterTrimWithFiles
            {
                QT = qt,
            };
            if (cfg.UploadConfig.Subscribe)
            {
                // qt without panels
                pl.QT = new QuarterTrim()
                {
                    Id = qt.Id,
                    DetectModelId = qt.DetectModelId,
                    Barcode = qt.Barcode,
                    Judge = qt.Judge,
                    Fixed = qt.Fixed,
                    CreatedDate = qt.CreatedDate,
                    No = qt.No,
                };
            }
            else if (cfg.UploadConfig.UploadSync)
            {
                foreach (var panel in qt.Panels)
                {
                    // include image base64
                    imgs.Add(panel.ResultImg);
                    imgs.Add(panel.BeforeImg);
                }
                List<FileWithBase64> Files = imgs.ConvertAll(img => new FileWithBase64
                {
                    Id = img.Id,
                    Path = img.Path,
                    Base64 = FileEntity.GetBase64(img.Path)
                });
                pl.Files = Files;
            }

            var data = new Notification
            {
                Id = qt.Id,
                Title = "OnQuarterTrimAdded",
                Description = pl.ToJson(),
            };
            return data;
        }

        public bool InfoAdded(BarcodeCounter bc)
        {
            // NOTE: if subscribe is true, not notify [OnBarcodeCounterAdded] mng via SignalR
            var cfg = DataClient.GetBaseConfig();
            if (cfg.UploadConfig.Subscribe)
            {
                return true;
            }

            var data = CreateNotification(bc);
            return Add(data);
        }

        internal static Notification CreateNotification(BarcodeCounter bc)
        {
            var data = new Notification
            {
                Title = "OnBarcodeCounterAdded",
                Description = bc.ToJson()
            };
            return data;
        }

        public override bool Post(Notification entity)
        {
#if false
            var cfg = DataClient.GetBaseConfig();
            // NOTE: if subscribe is true, not notify [OnBarcodeCounterAdded] mng via SignalR
            if (cfg.uploadConfig.subscribe)
            {
                if (entity.title == "OnBarcodeCounterAdded")
                {
                    return true;
                }
            }
#endif

            //string query = $"mutation CreateNotification($data:NotificationInput!){{createNotification(input: $data) {{id,title,description}}}}";
            //CreateNotificationData variables = new CreateNotificationData
            //{
            //    Data = entity
            //};
            //var b1 = GraphQueryZ(query, variables);

            var cmd = new
            {
                Event = entity.Title,
                Data = entity.Description
            };
            var b = DataClient.ApiPostCommand(cmd.ToJson());
            return b;
        }

    }
    public class UserRepository : TRepository<User>
    {
        public bool Login(User user)
        {
            var found = entitys.Find(u => u.Account.CompareTo(user.Account) == 0 && u.Password == user.Password);
            if (found != null)
            {
                Add(user);
            }
            return found != null;
        }
        public bool Fetch()
        {
            var ret = false;
            try
            {
                var di = new DirectoryInfo(path);
                foreach (var fi in di.GetFiles())
                {
                    string json = File.ReadAllText(fi.FullName);
                    User e = json.FromJson<User>();
                    bool ok = CheckLogin(e);
                    if (ok)
                    {
                        fi.Delete();
                    }
                    return ok;
                }
                ret = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
            return ret;
        }
    }
}
