using FR;
using Models;
using Services;
using System.Diagnostics;
using System.Text.RegularExpressions;
using WP_GUI.ModelAlgorithm;
using Yolov;
using static PanelHelpers;
using static Services.DataClient;
using Directory = System.IO.Directory;
using Font = System.Drawing.Font;

public static class ProgramHelpers
{
    public static WP_GUI.Config cfg;
    static QuarterTrim _qt;
    public static QuarterTrim Qt1 { get => _qt; }

    public static Stopwatch timePerPhase;

    public static IPreprocess preprocess;
    public static Yolov.YoloModelBase _yoloMode;

    public static IDictionary<int, string> _dict = new Dictionary<int, string>();

    public static IDictionary<string, string> _dict2 = new Dictionary<string, string>();

    public static IUploadService srv;
    public static DetectModel detectModel;

    public static string CurModelId => detectModel.Id;
    public static string CurBarcode => detectModel.PartNumber;

    // (ci.jpeg, idx)
    public static bool StartDetectZ(List<(string path, ImgListIndex idx)> lst)
    {
        try
        {
            var (sampleDir, capture_input, capture_output) = CreateFolder();

            var map = new Dictionary<ImgListIndex, (string pathXml, int i, Models.Panel panel)> {
                { ImgListIndex.firstLeft, ($"{sampleDir}\\output\\front_01.xml", 1, Qt1.Panels.ElementAt(0)) },
                { ImgListIndex.firstRight, ($"{sampleDir}\\output\\front_02.xml", 2, Qt1.Panels.ElementAt(1)) },
                { ImgListIndex.secondLeft, ($"{sampleDir}\\output\\back_01.xml", 3, Qt1.Panels.ElementAt(2)) },
                { ImgListIndex.secondRight, ($"{sampleDir}\\output\\back_02.xml", 4, Qt1.Panels.ElementAt(3)) },
            };

            var baseCfg = GetBaseConfig();
            var delta = baseCfg.DetectionSetting.Delta;
            var showPercent = baseCfg.ShowPercent;

            var sumRs = lst.ConvertAll(x =>
            {
                var sw = Stopwatch.StartNew();

                // crop
                var i = map[x.idx].i;
                var f = $"{capture_input}\\cc_{i}.jpeg";
                preprocess.Crop(x.path, f);

                sw.Stop();
                Logger.Debug($"Crop completed! {sw.ElapsedMilliseconds} ms");

                // detect
                sw = Stopwatch.StartNew();
                var xml = map[x.idx].pathXml;
                List<bool> res;
                List<ImgObject> outObj;
                (res, outObj) = DetectAndRestruct(f, xml, delta);
                sw.Stop();

                // process result
                var sumR = res.Any() && res.All(e => e);
                Logger.Debug($"ObjectDetect completed {map[x.idx].panel.BeforeImg.Id} {sumR} {sw.ElapsedMilliseconds} ms");

                // create output img
                var org = new Bitmap(x.path);
                string resultPath = DrawRectangleAndSave(org, new Font("Arial", 40), i, outObj, res, capture_output);
                // release img
                org.Dispose();

                var panel = map[x.idx].panel;
                Debug.Assert(panel.Type == PanelHelpers.ImgListIndexToPanelType(x.idx));

                // create parts
                // update panel.parts
                var parts = PartHelpers.CreatePartEntitys(outObj, res);
                panel.Parts = parts;

                return sumR;
            });

            // only 1 result include value is false => return false
            return sumRs.All(x => x);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            return false;
        }
    }

    private static (string sampleDir, string capture_input, string capture_output) CreateFolder()
    {
        var captureDir = cfg.CaptureDir;
        var sampleDir = cfg.SampleDir;
        var capture_input = $"{captureDir}\\input\\{Qt1.Id}";
        var capture_output = $"{captureDir}\\output\\{Qt1.Id}";
        System.IO.Directory.CreateDirectory(capture_input);//capture005//input//005_1
        System.IO.Directory.CreateDirectory(capture_output);//capture005//output//005_1
        return (sampleDir, capture_input, capture_output);
    }

    public static List<PredictionBox> RenameAndFilter(List<PredictionBox> boxes)
    {
        var reg = new Regex("^(ng|ig)_"); // NG or IGNORE

        // rename boxes
        boxes.ForEach(box => { if (_dict2.ContainsKey(box.LabelName)) { box.LabelName = _dict2[box.LabelName]; } });

        // filter OK boxes
        var okBoxes = boxes.Where(obj => !reg.IsMatch(obj.LabelName)).ToList();

        return okBoxes;
    }


    public static List<string> CreateOutput(string capture_output, IEnumerable<string> imgPaths, List<(List<bool>, List<ImgObject>, int)> listResult, bool showPercent = false)
    {
        List<string> coLst = new List<string>();
        List<Bitmap> listBitmap = new List<Bitmap>();
        foreach (string path in imgPaths)
        {
            listBitmap.Add(ImageExtensions.CloneFromFile(path));
        }
        // select detect method  1 or another number
        int option = 1;
        if (option == 1)
        {
            foreach (var (res, outObj, i) in listResult)
            {
                try
                {
                    string resultPath = DrawRectangleAndSave(listBitmap[i], new Font("Arial", 40), i + 1, outObj, res, capture_output);
                    coLst.Add(resultPath);
                }
                catch (Exception e)
                {
                    Logger.Error(e.Message);
                }
            }
        }
        else
        {
            foreach (var (res, outObj, i) in listResult)
            {
                // mark objects &
                // upload result imgs
                try
                {
                    CreateResultImg(listBitmap[i], new Font("Arial", 40), outObj, res, showPercent);
                }
                catch (Exception e)
                {
                    Logger.Error(e.Message);
                }
                string imgPath = SaveResultImg(capture_output, listBitmap[i], i + 1, res, outObj);
                coLst.Add(imgPath);
            }
        }
        //return listResult;
        listBitmap.ForEach(bmp => bmp.Dispose());

        return coLst;
    }

    public static void CreateResultImg(Bitmap btm, Font font, List<ImgObject> outObj, List<bool> res, bool showPercent = false)
    {
        for (int i = 0; i < outObj.Count; i++)
        {
            Brush color = res[i] ? Brushes.Green : Brushes.Red;
            Pen pen = new Pen(color, 12);
            // draw label in original images
            using (Graphics G = Graphics.FromImage(btm))
            {
                //detection rectangle
                int w = (int)outObj[i].W;
                int h = (int)outObj[i].H;
                Rectangle rec = new Rectangle((int)outObj[i].X, (int)outObj[i].Y, (int)outObj[i].W, (int)outObj[i].H);
                G.DrawRectangle(pen, rec);
            }
        }

        for (int i = 0; i < outObj.Count; i++)
        {
            Brush color = res[i] ? Brushes.Green : Brushes.Red;
            // draw label in original images
            using (Graphics G = Graphics.FromImage(btm))
            {
                //label rectangle
                var label = showPercent ? $"{outObj[i].LabelName}" : outObj[i].LabelName;
                SizeF stringSize = G.MeasureString(label, font);
                Rectangle labelRec = new Rectangle((int)outObj[i].X, (int)outObj[i].Y - 70, (int)stringSize.Width, (int)stringSize.Height);
                G.FillRectangle(color, labelRec);
                // label name
                G.DrawString(label, font, Brushes.White, (int)outObj[i].X - 15, (int)outObj[i].Y - 70);
            }
        }
    }


    public static string DrawRectangleAndSave(Bitmap imageFile, Font font, int index, List<ImgObject> outObjs, List<bool> res, string capture_output)
    {

        for (int i = 0; i < outObjs.Count(); i++)
        {
            using (Graphics G = Graphics.FromImage(imageFile))
            {

                Rectangle rec = GetFrameRec(outObjs[i]);
                DrawFrame(G, res[i], rec);
            }
        }
        for (int i = 0; i < outObjs.Count(); i++)
        {
            using (Graphics G = Graphics.FromImage(imageFile))
            {
                Rectangle labelRec = GetLabelRec(G, outObjs[i], font);
                Rectangle frameRec = GetFrameRec(outObjs[i]);
                labelRec = ProcessOverlap(font, outObjs, imageFile, G, outObjs[i], labelRec, frameRec, res[i]);
                DrawLabel(font, G, outObjs[i], labelRec, res[i]);
            }
        }

        string txtPath = Path.Combine(capture_output, $"co9_{index}.txt");
        string imgPath = Path.Combine(capture_output, $"co_{index}.jpeg");
        imageFile.Save(imgPath);
        string textJudge = string.Join("\t", res.ConvertAll(t => t ? "OK" : "NG"));
        string textScore = string.Join("\t", outObjs.ConvertAll(t => (t.score * 100).ToString()));
        string textName = string.Join("\t", outObjs.ConvertAll(t => t.LabelName));
        string text = textJudge + "\n" + textName + "\n" + textScore;
        File.WriteAllText(txtPath, text);
        return imgPath;
    }

    private static Rectangle ProcessOverlap(Font font, List<ImgObject> outObjs, Image imageFile, Graphics G, ImgObject part, Rectangle labelRec, Rectangle frameRec, bool res)
    {
        foreach (var otherPart in outObjs)
        {
            Rectangle otherPartRec = GetLabelRec(G, otherPart, font);
            if (otherPart != part && labelRec.IntersectsWith(otherPartRec))
            {
                var d = CalculateDistance(labelRec, otherPartRec);
                if (labelRec.X > otherPart.X)
                {
                    labelRec.X += d;
                }
                else if (labelRec.X < otherPart.X)
                {
                    labelRec.X -= (labelRec.Width - d);
                }
            }
        }
        foreach (var otherPart in outObjs)
        {
            Rectangle otherPartRec = GetFrameRec(otherPart);
            if (!IsInsideOtherRec(frameRec, otherPartRec) && otherPart != part && labelRec.IntersectsWith(otherPartRec))
            {
                var d = CalculateDistance(labelRec, otherPartRec);
                if (labelRec.X > otherPart.X)
                {
                    labelRec.X += d;
                }
                else if (labelRec.X < otherPart.X)
                {
                    labelRec.X -= (labelRec.Width - d);
                }
            }
        }
        labelRec.X = labelRec.X < 0 ? 0 : labelRec.X;
        labelRec.X = labelRec.X > imageFile.Width ? imageFile.Width : labelRec.X;
        labelRec.X = GetOverX(labelRec, frameRec);
        return labelRec;
    }

    private static int GetOverX(Rectangle labelRec, Rectangle frameRec)
    {
        return labelRec.X > frameRec.X + frameRec.Width ? frameRec.X + frameRec.Width : labelRec.X + labelRec.Width < frameRec.X ? frameRec.X - labelRec.Width : labelRec.X;
    }
    private static int CalculateDistance(Rectangle childRectangle, Rectangle parentRectangle)
    {
        int closestX = childRectangle.X < parentRectangle.X ? parentRectangle.X : parentRectangle.X + parentRectangle.Width;
        int closestY = childRectangle.Y;
        double distance = Math.Sqrt(Math.Pow(closestX - childRectangle.X, 2) + Math.Pow(closestY - childRectangle.Y, 2));
        return (int)distance;
    }

    private static void DrawLabel(Font font, Graphics G, ImgObject outObj, Rectangle labelRec, bool res)
    {
        G.FillRectangle(GetColor(res), labelRec);
        G.DrawString(outObj.LabelName, font, Brushes.White, labelRec.X, labelRec.Y);
    }

    private static void DrawFrame(Graphics G, bool res, Rectangle rec)
    {
        Pen pen = new Pen(GetColor(res), 12);
        G.DrawRectangle(pen, rec);
    }

    public static Brush GetColor(bool res)
    {
        Brush green = new SolidBrush(Color.FromArgb(128, 0, 128, 1));
        Brush red = new SolidBrush(Color.FromArgb(128, 255, 0, 0));
        return res ? green : red;
    }

    public static bool IsInsideOtherRec(Rectangle child, Rectangle parent)
    {
        return parent.Contains(child);
    }

    public static Rectangle GetFrameRec(PredictionBox outObj)
    {
        return new Rectangle((int)outObj.X, (int)outObj.Y, (int)outObj.W, (int)outObj.H);
    }

    public static Rectangle GetLabelRec(Graphics G, PredictionBox outObj, Font font)
    {
        var label = outObj.LabelName;
        SizeF stringSize = G.MeasureString(label, font);
        return new Rectangle((int)outObj.X, (int)outObj.Y - 70, (int)stringSize.Width, (int)stringSize.Height);
    }

    public static bool SwRT(string rt)
    {
        switch (rt)
        {
            case "openvino-auto":
                cfg.m_modelType = "openvino";
                cfg.m_deviceDetect = "auto";
                break;
            case "openvino-multi":
                cfg.m_modelType = "openvino";
                cfg.m_deviceDetect = "multi";
                break;
            case "FR-CPU":
                cfg.m_modelType = "faster_rcnn_resnet152";
                cfg.m_deviceDetect = "auto";
                break;
            case "FR-CUDA":
                cfg.m_modelType = "faster_rcnn_resnet152";
                cfg.m_deviceDetect = "cuda";
                break;
            default: return false;
        }

        foreach (var (_, model) in modelPool)
        {
            model.Dispose();
        }
        modelPool.Clear();
        detectModel = new Models.DetectModel();

        return true;
    }

    public static bool LoadModel(string barcode)
    {
        try
        {
            // if model change
            if (detectModel == null || detectModel.PartNumber != barcode)
            {
                // fetch model
                //var model = GetDetectModelByBarCode(barcode).Result;
                var model = ProgramHelpers.srv.GetDetectModelByBarCode(barcode);
                if (model == null)
                {
                    return false;
                }

#if false
            // check model file is downloaded
            if (!File.Exists(ProgramHelpers.cfg.ModelFile))
            {
                var ret = DownloadZ(model, ProgramHelpers.cfg.SampleOutDir);
                if (!ret)
                {
                    return false;
                }
            }
#endif

                // init new objectdetect model
                var newCfg = new WP_GUI.Config
                {
                    m_model = barcode,
                    m_modelType = cfg.m_modelType,
                    m_ModelConfidence = cfg.m_ModelConfidence,
                    m_deviceDetect = cfg.m_deviceDetect,
                    root = cfg.root,
                };

                var bOK = InitNewDetectModel(newCfg);
                if (!bOK) { return false; }

                //switch path model 
                ProgramHelpers.cfg.m_model = barcode;

                // if download ok => change model
                detectModel = model;

                ProgramHelpers.Qt1.DetectModelId = detectModel.Id;
                ProgramHelpers.Qt1.Barcode = barcode;
                ProgramHelpers.Qt1.Judge = true;
                ProgramHelpers.Qt1.Fixed = false;

                return true;
            }
            else
            {
                if (string.IsNullOrEmpty(CurModelId))
                {
                    return false;
                }

                ProgramHelpers.Qt1.DetectModelId = CurModelId;
                ProgramHelpers.Qt1.Barcode = barcode;
                ProgramHelpers.Qt1.Judge = true;
                ProgramHelpers.Qt1.Fixed = false;
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            return false;
        }
    }

    public static Queue<(string path, YoloModelBase model)> modelPool = new Queue<(string, YoloModelBase)>();
    private static bool InitNewDetectModel(WP_GUI.Config cfg)
    {
        try
        {
            string model = cfg.m_model;
            string modelType = cfg.m_modelType;
            float confidence = cfg.m_ModelConfidence;
            string sample_output = cfg.SampleOutDir;
            var device = cfg.m_deviceDetect;

            var modelConfigPath = Path.Combine(sample_output, "data_sample.xlsx");
            var dictJson = Path.Combine(sample_output, "dict.json");

#if false
                if (ProgramHelpers._objDetector != null)
                {
                    ProgramHelpers._objDetector.Dispose();
                    ProgramHelpers._objDetector = null;
                }
                ProgramHelpers._objDetector = new ObjectDetector<Retinanet>(modelPath);
#endif
            if (ProgramHelpers._yoloMode != null)
            {
                //ProgramHelpers._yoloMode.Dispose();
                ProgramHelpers._yoloMode = null;
            }

            // default model
            var type = "";
            var path = "";

            // specify FR model
            var lst = GetModles(sample_output);

            // find model
            try
            {
                switch (modelType)
                {
                    case "openvino":
                        type = nameof(VinoYolo8);
                        break;
                    case "faster_rcnn_resnet152":
                        type = nameof(FasterRetinanet);
                        break;
                    case "yolov8x":
                        type = nameof(YoloModel8);
                        break;
                    default:
                        type = lst.First().Item3;
                        break;
                }
                path = lst.First((x) => x.Item3 == type).Item2;
            }
            catch
            {
                Logger.Error("Invalid config.modelType");
            }

            if (string.IsNullOrEmpty(path)) { return false; }

            if (!modelPool.Any(x => x.path == path))
            {
                // release old_model
                if (modelPool.Count > 1)
                {
                    modelPool.Dequeue().model.Dispose();
                }

                // create model
                switch (type)
                {
                    case nameof(VinoYolo8):
                        ProgramHelpers._yoloMode = new VinoYolo8
                        {
                            Overlap = 0.45f,
                            ModelConfidence = confidence,
                            DeviceDetect = device,
                        };
                        break;
                    case nameof(FasterRetinanet):
                        ProgramHelpers._yoloMode = new FasterRetinanet
                        {
                            Overlap = 0.45f,
                            ModelConfidence = confidence,
                            DeviceDetect = device,
                        };
                        break;
                    case nameof(YoloModel8):
                        ProgramHelpers._yoloMode = new YoloModel8
                        {
                            Overlap = 0.45f,
                            ModelConfidence = confidence,
                            DeviceDetect = device,
                        };
                        break;
                }

                // load model
                ProgramHelpers._yoloMode.Detect(null, path);

                modelPool.Enqueue((path, ProgramHelpers._yoloMode));
            }
            else
            {
                ProgramHelpers._yoloMode = modelPool.First(x => x.path == path).model;
            }

            // load dict
            if (File.Exists(dictJson))
            {
                (ProgramHelpers._dict, ProgramHelpers._dict2) = ReadDict(dictJson);
            }
            else
            {
                throw new Exception("Error dict.json not found");
            }

            // auto crop
            //ProgramHelpers.preprocess.UpdateCropModel(ProgramHelpers.cfg);

            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e.Message);
            return false;
        }
    }

    public static string GetModelType(DirectoryInfo sub)
    {
        var type = nameof(YoloModel8);
        if (Regex.IsMatch(sub.Name, "^FR$|^FR_"))
        {
            type = nameof(FasterRetinanet);
        }
        else if (Regex.IsMatch(sub.Name, "^openvino$|^openvino_"))
        {
            type = nameof(VinoYolo8);
        }

        return type;
    }


    public static List<(string, string, string)> GetModles(string dir)
    {
        var d = new DirectoryInfo(dir);
        var lst = new List<(string, string, string)>();
        if (d.Exists)
        {
            foreach (var sub in d.GetDirectories().ToList())
            {
                var f = sub.GetFiles("*.*")
                .Where(x => x.Name.EndsWith(".onnx") || x.Name.EndsWith(".xml"))
                .FirstOrDefault();
                if (f != null)
                {
                    string type = GetModelType(sub);
                    lst.Add((sub.Name, f.FullName, type));
                }
            }
        }
        return lst;
    }

    public class LabelExt
    {
        public string label;
        public int idx;
        public float score;
        public string alias;
    }
    public static (Dictionary<int, string> l_idxLabelDict, Dictionary<string, string> l_labelAliasDict) ReadDict(string path)
    {
        var txt = File.ReadAllText(path);

        var l_idxLabelDict = new Dictionary<int, string>();
        var l_labelAliasDict = new Dictionary<string, string>();

        var lst = txt.FromJson<List<LabelExt>>();
        foreach (var item in lst)
        {
            l_idxLabelDict.Add(item.idx, item.label);
            l_labelAliasDict.Add(item.label, item.alias);
        }

        return (l_idxLabelDict, l_labelAliasDict);
    }
    public static List<(List<bool>, List<ImgObject>, int)> DetectAndRestruct(List<string> listPaths, IEnumerable<string> pathXml)
    {
        //detect image 
        //Bitmap img = new Bitmap(ci1);
        //int img_w = img.Width; int img_h = img.Height;
        //List<DenseTensor<float>[]> results = _objDetector.Detect(ci1);
        var listResult = new List<(List<bool>, List<ImgObject>, int)>();
        var detectionSetting = GetBaseConfig().DetectionSetting;
        //var accuracy = detectionSetting.Accuracy;
        var delta = detectionSetting.Delta;

        for (int i = 0; i < listPaths.Count; i++)
        {
            try
            {
                var xml = pathXml.ElementAt(i);
                var path = listPaths[i];
                var (res, outObj) = DetectAndRestruct(path, xml, delta);
                listResult.Add((res, outObj, i));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }

        return listResult;
    }

    public static (List<bool> res, List<ImgObject> outObj) DetectAndRestruct(string path, string pathXml, float delta)
    {
        var sw = Stopwatch.StartNew();
        var pbc = _yoloMode.Detect(path);
        sw.Stop();
        Logger.Debug($"Detect completed! {sw.ElapsedMilliseconds} ms");

        sw = Stopwatch.StartNew();
        var (res, outObj) = preprocess.FilterAndRestruct(delta, pathXml, pbc, path);
        sw.Stop();
        Logger.Debug($"FilterAndRestruct completed! {sw.ElapsedMilliseconds} ms");
        return (res, outObj);
    }

    public static float Distance(PointF p1, PointF p2)
    {
        var (dx, dy) = (p1.X - p2.X, p1.Y - p2.Y);
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static string SaveResultImg(string capture_output, Bitmap btm, int stt, List<bool> res, List<ImgObject> outObjs)
    {
        string txtPath = Path.Combine(capture_output, $"co9_{stt}.txt");
        string imgPath = Path.Combine(capture_output, $"co_{stt}.jpeg");
        btm.Save(imgPath);
        //System.IO.File.WriteAllText(txtPath, string.Join(", ", listJdg.ConvertAll(t => t.Item2 ? "OK" : "NG")));
        string textJudge = string.Join("\t", res.ConvertAll(t => t ? "OK" : "NG"));
        string textScore = string.Join("\t", outObjs.ConvertAll(t => (t.score * 100).ToString()));
        string textName = string.Join("\t", outObjs.ConvertAll(t => t.LabelName));
        string text = textJudge + "\n" + textName + "\n" + textScore;
        File.WriteAllText(txtPath, text);
        return imgPath;
    }

    public static void UploadResult()
    {
        var bNG = Qt1.Panels.Any(panel => panel.Parts == null || panel.Parts.Any(part => part.Judge == false));

        // create &
        // send quartertrim
        ProgramHelpers.Qt1.Judge = !bNG;
        ProgramHelpers.Qt1.CreatedDate = DateTimeOffset.Now;
        //foreach (var panel in qt1.Panels)
        //{
        //    panel.QuarterTrimId = ProgramHelpers.qt1.Id;
        //}

        srv.Add(ProgramHelpers.Qt1);
    }

    public static string GetTodayDateString()
    {
        return DateTime.Now.ToString("yyyyMMdd");
    }
    public static string CreateJsonFile(string path, string json)
    {
        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
        System.IO.File.WriteAllText(path, json);
        return dir;
    }

    public static void LoadConfig()
    {
        var baseCfg = GetBaseConfig();
        cfg = new WP_GUI.Config
        {
            root = baseCfg.WorkingDir,
            m_modelType = baseCfg.ModelDetect,
            m_ModelConfidence = baseCfg.DetectionSetting.Accuracy,
            m_deviceDetect = baseCfg.DeviceDetect,
        };
        return;
    }

    internal static void InitSession(string sectionId)
    {
        var panels = new List<Models.Panel> {
                        new Models.Panel{ Type = PanelType.FIRST_FRONT },
                        new Models.Panel{ Type = PanelType.SECOND_FRONT },
                        new Models.Panel{ Type = PanelType.FIRST_BACK },
                        new Models.Panel{ Type = PanelType.SECOND_BACK },
                    };
        _qt = new QuarterTrim
        {
            Id = sectionId,
            Panels = panels
        };
        preprocess.InitSession(_qt);
    }
}