using ClosedXML.Excel;
using comp;
using Extensions;
using FR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Yolov;
using static annotation.BgWrk;
using static annotation.Form1;

namespace annotation
{
    public class AnntShell
    {

        public class MyImg
        {
            public string path;

            public bool hasLabel;
        }
        public class MyData
        {
            public string path; // train|val
            public List<MyImg> images;
        }
        public static void InitColorPalette(AnntShellEnv _env)
        {
            _env.colorPalette.Init(_env.labelConfig);
        }
        public static LabelConfig ReadLabelConfig(string dir)
        {
            var labelConfig = new LabelConfig();
            var cfg = new DirectoryInfo(dir).GetFiles("*.yaml").FirstOrDefault();
            if (cfg != null)
            {
                labelConfig._dict = Yolov.Notation.ReadLabelDict(cfg.FullName);
                if (labelConfig._dict == null) { throw new Exception("Invalid config!"); }
            }

            return labelConfig;
        }
        private static HashSet<string> GetHash(string labelD)
        {
            return new DirectoryInfo(labelD)
                .GetFiles("*.txt")
                .Select(f => Path.GetFileNameWithoutExtension(f.Name))
                .ToHashSet();
        }

        public static List<MyData> ReadData(AnntShellEnv env, string dir)
        {
            // C:\work\investigate\yolov5\datasets\dd\data607
            // images
            //   train
            //   val
            // labels
            //   train
            //   val
            var imagesD = new DirectoryInfo(Path.Combine(dir, "images"));
            if (!imagesD.Exists) return null;
            var lst = imagesD.GetDirectories().ToList().ConvertAll(
            di =>
            {
                var newData = new MyData { path = di.FullName };
                var files = di.GetFiles("*.*").Where(f => Regex.IsMatch(f.Extension, "^\\.(jpeg|png)$", RegexOptions.IgnoreCase)).ToList();
                var labelD = di.FullName.Replace("images", "labels");
                var h = Directory.Exists(labelD) ? GetHash(labelD) : new HashSet<string>();
                newData.images = files.ToList().ConvertAll((file) => new MyImg
                {
                    path = file.FullName,
                    hasLabel = h.Contains(Path.GetFileNameWithoutExtension(file.Name))
                });
                return newData;
            });

            return lst;
        }


        public static AnntNode Explore(AnntShellEnv env, ReportCallback cb = null)
        {
            var di = new DirectoryInfo(env._wkDir.YOLODataset);
            AnntNode root = new AnntNode(di);

            // read data
            var lst = ReadData(env, root.di.FullName);
            if (lst == null) return null;

            // transform
            root._nodes = lst.ConvertAll(data =>
            {
                var ts_x = new AnntNode(new DirectoryInfo(data.path))
                {
                    _parent = root,
                };
                ts_x._nodes = data.images.ConvertAll(img =>
                {
                    var newNode = new AnntNode(new FileInfo(img.path))
                    {
                        _parent = ts_x,
                        state = img.hasLabel ? 1 : 2,
                        Size = 1 // incre parent count
                    };
                    cb?.Invoke(newNode);
                    return newNode;
                });
                return ts_x;
            });
            return root;
        }
        public static AnntNode ExploreRoot(AnntShellEnv env, ReportCallback cb = null)
        {
            var di = new DirectoryInfo(env._wkDir.YOLODataset);
            AnntNode root = new AnntNode(di);

            // YOLODataset\images\<subDir>
            var imagesD = new DirectoryInfo(Path.Combine(di.FullName, "images"));
            if (!imagesD.Exists) return null;

            var lst = imagesD.GetDirectories().ToList();
            root._nodes = lst.ConvertAll(sub => new AnntNode(sub) { _parent = root, });
            return root;
        }

        public static void ExploreSub(AnntShellEnv env, AnntNode n, ReportCallback cb = null)
        {
            var di = n.di;
            var files = di.GetFiles("*.*").Where(f => Regex.IsMatch(f.Extension, "^\\.(jpeg|png)$", RegexOptions.IgnoreCase)).ToList();
            var labelD = di.FullName.Replace("images", "labels");
            var h = Directory.Exists(labelD) ? GetHash(labelD) : new HashSet<string>();
            n._nodes = files.ConvertAll((file) =>
            {
                var hasLabel = h.Contains(Path.GetFileNameWithoutExtension(file.Name));
                var newNode = new AnntNode(file)
                {
                    _parent = n,
                    state = hasLabel ? 1 : 2,
                    Size = 1
                };
                cb?.Invoke(newNode);
                return newNode;
            });
        }

        public static AnntNode Explore2(AnntShellEnv env)
        {
            var di = new DirectoryInfo(env._wkDir.YOLODataset);
            AnntNode root = new AnntNode(di);

            var r = new Regex(@"^((\w+) \((\d+)\)|(\w+)_(\d{3}))$");

            // read data and dict
            var lst = ReadData(env, root.di.FullName);
            if (lst == null) return null;

            // group
            root._nodes = new List<AnntNode>();
            lst.ForEach(data =>
            {
                var tsname = Path.GetFileName(data.path);
                var lstts = data.images.Select(img =>
                {
                    var group = tsname;
                    var imgName = Path.GetFileNameWithoutExtension(img.path);
                    var n = 0;
                    var m = r.Match(imgName);
                    if (m.Success)
                    {
                        group = m.Groups[2].Value + m.Groups[4].Value;
                        n = int.Parse(m.Groups[3].Value + m.Groups[5].Value);
                    }
                    return new { group, n, img };
                })
                .GroupBy((n => n.group), (gname, objs) =>
                {
                    var ts_x = new AnntNode(new DirectoryInfo(data.path))
                    {
                        _parent = root,
                    };

                    if (!ts_x._name.Contains(gname))
                    {
                        ts_x._name += "_" + gname;
                    }
                    ts_x._nodes = objs.OrderBy(obj => obj.n)
                    .Select(obj =>
                    {
                        return new AnntNode(new FileInfo(obj.img.path))
                        {
                            _parent = ts_x,
                            state = obj.img.hasLabel ? 1 : 2,
                            Size = 1 // incre parent count
                        };
                    })
                    .ToList();
                    return ts_x;
                })
                .ToList();
                root._nodes.AddRange(lstts);
            });
            return root;
        }

        public static int Detect(AnntShellEnv env, AnntNode rootNode, ReportCallback cb = null, bool overwrite = false)
        {
            var addedLabels = new List<string>();
            var modelPath = env.cfg.ModelPath;
            if (env.yolo == null || modelPath != env.yoloModelPath)
            {
                CreateModel(env);
                env.yoloModelPath = modelPath;
                env.yolo.Detect(null, modelPath);

                // update dict
                var lst = env.yolo.labelDict.Values;
                addedLabels = AddLabelsToConfig(env, lst);

                if (addedLabels.Count > 0)
                {
                    Logger.Logger.Debug($"Added labels {string.Join(",", addedLabels)}");
                }
            }

            env._traverser.Start(rootNode, (node) =>
            {
                var txt = node.lPath;
                var bExist = File.Exists(txt);
                if (overwrite || !bExist)
                //if (node.state == 2)
                {
                    AutoRect(env, node, env.cfg.autoRect_autoSave);

                    cb?.Invoke(node);
                }
                return true;
            }, node => node.isFile);
            return addedLabels.Count;
        }

        public static void CreateModel(AnntShellEnv env)
        {
            switch (env.cfg.ModelType)
            {
                case nameof(VinoYolo8):
                    env.yolo = new VinoYolo8
                    {
                        Overlap = 0.45f,
                        ModelConfidence = env.cfg.ModelConfidence,
                    };
                    break;
                case nameof(YoloModel8):
                    env.yolo = new YoloModel8
                    {
                        Overlap = 0.45f,
                        ModelConfidence = env.cfg.ModelConfidence,
                    };
                    break;
                case nameof(FasterRetinanet):
                    env.yolo = new FasterRetinanetCV
                    {
                        Overlap = 0.45f,
                        ModelConfidence = env.cfg.ModelConfidence,
                    };
                    break;
            }
        }

        public static List<string> AddLabelsToConfig(AnntShellEnv env, IEnumerable<string> lst)
        {
            var addedLabels = new List<string>();
            var idx = env.labelConfig._dict.Count == 0 ? 0 : env.labelConfig._dict.Keys.Max() + 1;
            foreach (var p in lst)
            {
                if (!env.labelConfig._dict.Values.Contains(p))
                {
                    env.labelConfig._dict.Add(idx, p);
                    idx++;
                    addedLabels.Add(p);
                }
            }

            return addedLabels;
        }

        public static bool AutoRect(AnntShellEnv env, AnntNode node, bool autoRect_autoSave = true)
        {
            // Debug.Assert(node.state == 2);
            {
                var file = node._parent.di.FullName + "\\" + node._name;
                var pbc = env.yolo.Detect(file, null);
                node.state = 3;
                node.bmp = (pbc.w, pbc.h);
                node.objs = pbc.boxes.ConvertAll(pb =>
                {
                    var id = pb.label.id;
                    var name = pb.label.name;
                    //if (string.IsNullOrEmpty(name) && labelConfig._dict.ContainsKey(id))
                    //{
                    //    name = labelConfig._dict[id];
                    //}
                    return new MyBox
                    {
                        LabelName = name,
                        Id = id,
                        yoloBox = pb.rectangle.ToYoloBox(node.bmp.Width, node.bmp.Height),
                        bmp = (node.bmp.Width, node.bmp.Height),
                        percent = (int)(pb.score * 100),
                    };
                });

                if (autoRect_autoSave)
                {
                    var (path, path2) = SaveTxt(env, node);
                    if (!string.IsNullOrEmpty(path))
                    {
                        // save ok
                        node.state = 1; // done
                    }
                }
                else
                {
                    node.state = 3; // edit
                }

                return true;
            }

        }

        public static (string, string) SaveTxt(AnntShellEnv env, AnntNode node, bool export_yolo = true, bool export_xml = false)
        {
            try
            {
                string lPath = null;
                string xml = null;
                if (export_yolo)
                {
                    var lDir = node._parent.di.FullName.Replace("images", "labels");
                    lPath = Path.Combine(lDir, Path.GetFileNameWithoutExtension(node._name) + ".txt");
                    if (!Directory.Exists(lDir))
                    {
                        Directory.CreateDirectory(lDir);
                    }
                    List<string> lines = ExportToYolo(env, node);
                    File.WriteAllLines(lPath, lines);
                }

                // export xml
                if (export_xml)
                {
                    xml = Path.Combine(node._parent.di.FullName, Path.GetFileNameWithoutExtension(node._name) + ".xml");
                    var xmlTxt = Parser.ExportXml(node._parent.di.Name, filename: node._name,
                        path: node._parent.di.FullName + "\\" + node._name, node.objs);
                    File.WriteAllText(xml, xmlTxt);
                }

                return (lPath, xml);
            }
            catch (Exception ex)
            {
                Logger.Logger.Error(ex.Message);
                return (null, null);
            }
        }

        public static List<string> ExportToYolo(AnntShellEnv env, AnntNode n)
        {
            var (w, h) = (n.bmp.Width, n.bmp.Height);
            var lines = n.objs.ConvertAll(obj =>
            {
                var box = obj.yoloBox;
                var idx = env.labelConfig._dict.First(p => p.Value == obj.LabelName);
                var line = $"{idx.Key} {box.cx} {box.cy} {box.width} {box.height}";
                if (!obj.IsRect)
                {
                    line = $"{idx.Key} {string.Join(" ", obj.Points.Select(p => $"{p.X / w} {p.Y / h}"))}";
                }
                return line;
            });
            return lines;
        }


        public static void MergeXml()
        {

            var src = @"D:\Sync_Door\DoorScript\data_set\model_7230B087XA\backup\001";
            var des = "D:\\yolo\\model_7230B087XA\\label";
            var bak = "D:\\yolo\\model_7230B087XA\\.bak";
            var di = new DirectoryInfo(des);
            var same = 0;
            int diff = 0;
            foreach (var sub in di.GetDirectories())
            {
                foreach (var xml in sub.GetFiles("*.xml"))
                {
                    var txt = File.ReadAllText(xml.FullName);
                    var srcF = Path.Combine(src, xml.Name);
                    if (File.Exists(srcF))
                    {
                        var txt2 = File.ReadAllText(srcF);
                        if (txt == txt2)
                        {
                            same++;
                        }
                        else
                        {
                            // bak
                            var bakDir = Path.Combine(bak, sub.Name);
                            if (!Directory.Exists(bakDir)) { Directory.CreateDirectory(bakDir); }
                            File.Move(xml.FullName, Path.Combine(bakDir, xml.Name));

                            // write
                            File.Copy(srcF, xml.FullName);

                            diff++;
                        }
                    }
                }
            }
            Logger.Logger.Debug($"{diff}/{diff + same}");
        }

        public static List<string> ParsePbtxt(string path)
        {
            // parse
            var dict = FasterRetinanet.ReadLabelDict(path);
            return dict.Values.ToList();
        }

        public static Bitmap Crop(Bitmap org, Rectangle _rec)
        {
            Bitmap crop = new Bitmap(org.Width, org.Height);
            using (Graphics gfx = Graphics.FromImage(crop))
            {
                using (SolidBrush brush = new SolidBrush(Color.Gray))
                {
                    gfx.FillRectangle(brush, 0, 0, org.Width, org.Height);
                }
                gfx.DrawImage(org, _rec, _rec, GraphicsUnit.Pixel);
            };
            return crop;
        }

        internal static void ExportLabels(AnntShellEnv env)
        {
            File.WriteAllText(
                env._wkDir.names_txt,
                env.labelConfig._dict.Values.ToList().ToJson()
                );
        }

        public static void UpdateDs(AnntShellEnv env)
        {
            var lst = env.labelConfig._dict.Values;
            var dict = new Dictionary<string, int>();
            foreach (var item in lst)
            {
                dict.Add(item, dict.Count);
            }
            UpdateDs(env._wkDir.dataset_yaml, dict);
        }

        public static void UpdateDs(string ds, Dictionary<string, int> dict)
        {
            var lines = File.ReadAllLines(ds);
            var newCfg = new List<string>();
            foreach (var line in lines)
            {
                newCfg.Add(line);
                if (Regex.IsMatch(line, "^names:"))
                {
                    break;
                }
            };
            foreach (var p in dict)
            {
                newCfg.Add($"  {p.Value}: {p.Key}");
            }

            // over-write
            File.WriteAllLines(ds, newCfg);
        }

        internal static void Split(AnntShellEnv env, List<string> subs = null)
        {
            var dir = $@"{env._wkDir.YOLODataset}\images";
            if (subs == null)
            {
                subs = new List<string>() {
                "AI", "AO",
                "BI", "BO"
            };
            }

            var dict = subs.ConvertAll(s => (s, "val_" + s, "test_" + s));
            var rate = 10;
            var rate2 = 10;
            var lDir = dir.Replace("images", "labels");

            foreach (var (src, dst, test) in dict)
            {
                var srcd = new DirectoryInfo(Path.Combine(dir, src));
                if (!srcd.Exists) { continue; }

                var vald = Path.Combine(dir, dst);
                if (!Directory.Exists(vald))
                {
                    Directory.CreateDirectory(vald);
                }
                var testd = Path.Combine(dir, test);
                if (!Directory.Exists(testd))
                {
                    Directory.CreateDirectory(testd);
                }
                var lst = srcd.GetFiles("*.jpeg");
                int i = calcN(rate, vald, lst);
                var i2 = calcN(rate2, testd, lst); ;
                var lst1 = lst.Take(i);
                IEnumerable<FileInfo> lst2 = lst.Skip(i).Take(i2);
                movefile(lDir, src, dst, vald, lst1);
                movefile(lDir, src, test, testd, lst2);
            }

        }
        static int movefile(string lDir, string src, string dst, string dstd, IEnumerable<FileInfo> lst)
        {
            int i = 0;
            foreach (var f in lst)
            {
                var key = Path.GetFileNameWithoutExtension(f.Name);

                // label
                var labelFile = Path.Combine(lDir, src, key + ".txt");
                Debug.Assert(File.Exists(labelFile));

                var newFile = Path.Combine(dstd, f.Name);
                f.MoveTo(newFile);

                var newLabelDir = Path.Combine(lDir, dst);
                var newLabelFile = Path.Combine(newLabelDir, key + ".txt");
                if (!Directory.Exists(newLabelDir)) { Directory.CreateDirectory(newLabelDir); }
                File.Move(labelFile, newLabelFile);

                i++;
            }
            Console.WriteLine($"moved {i}");
            return i;
        }

        static int calcN(int rate, string dstd, FileInfo[] lst)
        {
            var n = new DirectoryInfo(dstd).GetFiles("*.jpeg").Length;
            var i = 0;
            i = ((lst.Length + n) / rate) - n;
            i = i + 3;
            i = i - (i % 4);
            return i;
        }


        internal static void CopyDir(string pretrained_model_path_org, string outDir)
        {
            var dir = new DirectoryInfo(pretrained_model_path_org);
            var dest = new DirectoryInfo(outDir);
            CopyDirR(dir, dest);
        }

        private static void CopyDirR(DirectoryInfo src, DirectoryInfo dest)
        {
            foreach (var sub in src.GetDirectories())
            {
                var newDir = dest.CreateSubdirectory(sub.Name);
                CopyDirR(sub, newDir);
            }
            foreach (var file in src.GetFiles())
            {
                file.CopyTo(Path.Combine(dest.FullName, file.Name), true);
            }
        }

        internal static void CleanDir(string models)
        {
            var dir = new DirectoryInfo(models);
            CleanDirR(dir);
        }

        private static void CleanDirR(DirectoryInfo di)
        {
            foreach (var sub in di.GetDirectories())
            {
                CleanDirR(sub);
            }
            foreach (var file in di.GetFiles())
            {
                file.Delete();
            }
        }

        public class LabelExt
        {
            public string label;
            public int idx;
            public float score;
            public string alias;
        }

        public class ExportModelConfig
        {
            public List<LabelExt> labels;
            public List<(AnntNode, MyBox)> nodes;
            public string model;
            public bool createPng;
            public bool createXml;
            public bool copyModel;
            internal bool createDict;
            internal bool copyTestImg;
            internal bool updateModelsJson;
        }

        internal static string ExportModel(AnntShellEnv env, string outDir, ExportModelConfig cfg)
        {
            try
            {
                var model = env._wkDir.ModelName;
                var dir = Path.Combine(outDir, $@"model_{model}\sample_{model}\output");
                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

                // export to dict.json
                if (cfg.createDict)
                {
                    var dictjson = Path.Combine(dir, "dict.json");
                    if (File.Exists(dictjson))
                    {
                        var dictjson_bak = dictjson + ".bak";
                        if (File.Exists(dictjson_bak)) { File.Delete(dictjson_bak); }
                        File.Move(dictjson, dictjson_bak);
                    }
                    var content = cfg.labels.ToJson();
                    File.WriteAllText(dictjson, content);
                }

                // copy font.xml & back.xml
                if (cfg.createXml)
                {
                    new List<((AnntNode, MyBox), string)>() {
                        (cfg.nodes[0], "front_01.xml"),
                        (cfg.nodes[1], "front_02.xml"),
                        (cfg.nodes[2], "back_01.xml"),
                        (cfg.nodes[3], "back_02.xml"),
                    }.ForEach(p =>
                    {
                        var ((n, seg), name) = p;
                        var xml = Path.Combine(dir, name);
                        var segmented = "";
                        if (seg != null)
                        {
                            var rect = seg.Rect;
                            var (angle, area, cx, cy) = PlotCV.GetContourInfo(seg.points.Select(x => new OpenCvSharp.Point((int)x.X, (int)x.Y)));
                            segmented = $"{rect.X} {rect.Y} {rect.Width} {rect.Height} {angle} {area} {cx} {cy}";
                        }
                        var xmlTxt = Parser.ExportXml("", n.objs, segmented);
                        File.WriteAllText(xml, xmlTxt);
                    });
                }

                // front & back.png
                if (cfg.createPng)
                {
                    new List<(AnntNode, string)>()
                    {
                        (cfg.nodes[0].Item1, "front.png"),
                        (cfg.nodes[2].Item1, "back.png"),
                    }
                    .ForEach(p =>
                    {
                        var (n, name) = p;
                        var png = Path.Combine(dir, name);
                        if (File.Exists(png)) { return; }
                        var original = ImageExtensions.CloneFromFile(GetImgPath(n));
                        Bitmap resized = new Bitmap(original, new System.Drawing.Size(original.Width / 4, original.Height / 4));
                        resized.Save(png);
                    });
                }

                // update models.json
                if (cfg.updateModelsJson)
                {
                    var modelsJsonPath = Path.Combine(outDir, @"DB\models.json");
                    var modelsJson = File.ReadAllText(modelsJsonPath);
                    var models = modelsJson.FromJson<List<DetectModel>>();
                    if (!models.Exists(m => m.PartNumber == model))
                    {
                        var newModel = new DetectModel()
                        {
                            PartNumber = model,
                        };
                        models.Add(newModel);
                        File.WriteAllText(modelsJsonPath, models.ToJson());
                    }
                }

                // copy test img
                if (cfg.copyTestImg)
                {
                    var testdir = Path.Combine(outDir, $@"model_{model}\sample_{model}\test");
                    if (!Directory.Exists(testdir)) { Directory.CreateDirectory(testdir); }
                    foreach (var (n, _) in cfg.nodes)
                    {
                        var img = GetImgPath(n);
                        File.Copy(img, Path.Combine(testdir, n.PhysicName), true);
                    }
                }

                // copy model
                if (cfg.copyModel)
                {
                    var onnx = Path.Combine(dir, $"model_{model}.onnx");
                    File.Copy(cfg.model, onnx, true);
                }

                return dir;
            }
            catch (Exception ex)
            {
                Logger.Logger.Error(ex.Message);
                return null;
            }
        }



        internal static string ExportModel(AnntShellEnv env, string outDir, List<AnntNode> nodes)
        {
            try
            {
                var model = env._wkDir.ModelName;
                var dir = Path.Combine(outDir, $@"model_{model}\sample_{model}\output");
                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

                var frDir = Path.Combine(env._wkDir.models, "FR");
                var frFile = new DirectoryInfo(frDir).GetFiles("*.onnx").First();
                var labelFile = Path.Combine(frDir, "label_map.pbtxt");
                var dict = FasterRetinanet.ReadLabelDict(labelFile);

                // export data_sample.xlsx
                //var xlsx = Path.Combine(dir, "data_sample.xlsx");
                //CreateDictModelXlsx(xlsx, dict);

                // export to dict.json
                var dictjson = Path.Combine(dir, "dict.json");
                var content = dict
                    .Select(p => new
                    {
                        idx = (p.Key + 1),
                        label = p.Value,
                        score = 0.6,
                        alias = p.Value
                    })
                    .ToJson();
                File.WriteAllText(dictjson, content);

                // copy font.xml & back.xml
                new List<(AnntNode, string)>() {
                (nodes[2], "front_01.xml"),
                (nodes[0], "back_01.xml"),
                (nodes[3], "front_02.xml"),
                (nodes[1], "back_02.xml"),
            }.ForEach(p =>
            {
                var (n, name) = p;
                var xml = Path.Combine(dir, name);
                var xmlTxt = Parser.ExportXml(n._parent.di.Name, filename: n._name,
                        path: n._parent.di.FullName + "\\" + n._name, n.objs);
                File.WriteAllText(xml, xmlTxt);
            });

                // front & back.png
                new List<(AnntNode, string)>()
                {
                (nodes[2], "front.png"),
                (nodes[0], "back.png"),
                }
                .ForEach(p =>
                {
                    var (n, name) = p;
                    var png = Path.Combine(dir, name);
                    if (File.Exists(png)) { return; }
                    var original = ImageExtensions.CloneFromFile(GetImgPath(n));
                    Bitmap resized = new Bitmap(original, new System.Drawing.Size(original.Width / 4, original.Height / 4));
                    resized.Save(png);
                });

                // update models.json
                var modelsJsonPath = Path.Combine(outDir, @"DB\models.json");
                var modelsJson = File.ReadAllText(modelsJsonPath);
                var models = modelsJson.FromJson<List<DetectModel>>();
                if (!models.Exists(m => m.PartNumber == model))
                {
                    var newModel = new DetectModel()
                    {
                        PartNumber = model,
                    };
                    models.Add(newModel);
                    File.WriteAllText(modelsJsonPath, models.ToJson());
                }

                // copy test img
                var testdir = Path.Combine(outDir, $@"model_{model}\sample_{model}\test");
                if (!Directory.Exists(testdir)) { Directory.CreateDirectory(testdir); }
                foreach (var n in nodes)
                {
                    var img = GetImgPath(n);
                    File.Copy(img, Path.Combine(testdir, n.PhysicName), true);
                }

                // copy model
                var onnx = Path.Combine(dir, $"model_{model}.onnx");
                frFile.CopyTo(onnx, true);

                return dir;
            }
            catch (Exception ex)
            {
                Logger.Logger.Error(ex.Message);
                return null;
            }
        }

        private static void CreateDictModelXlsx(string xlsx, Dictionary<int, string> dict)
        {
            var cells = new List<(int row, int col, string val)>() {
                    (1,2,"obj"),
                    (1,3,"score"),
                };

            {
                var iRow = 2;
                foreach (var p in dict)
                {
                    cells.Add((iRow, 1, (p.Key + 1).ToString()));
                    cells.Add((iRow, 2, p.Value));
                    cells.Add((iRow, 3, "0.6"));
                    cells.Add((iRow, 4, p.Value));
                    iRow++;
                }
            }

            var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("DictModel");
            foreach (var (iRow, iCol, v) in cells)
            {
                ws.Cell(iRow, iCol).Value = v;
            }
            wb.SaveAs(xlsx);
        }

        public static string GetImgPath(AnntNode n)
        {
            return n.fi.FullName;
        }

        public static bool ExportPose(AnntShellEnv env, AnntNode rootNode, string dir, Action<object> cb, bool checkedOnly)
        {
            var cfg = env.cfg;
            var bmp = (cfg.imageSize[0], cfg.imageSize[1]);
            env._traverser.Start(rootNode, node =>
            {
                LoadOrInitBoxes(env, node, bmp);
                node.objs.Sort((a, b) =>
                {
                    var ret = a.LabelName.CompareTo(b.LabelName);
                    if (ret == 0)
                    {
                        ret = a.yoloBox.cx.CompareTo(b.yoloBox.cx);
                        if (ret == 0)
                        {
                            ret = a.yoloBox.cy.CompareTo(b.yoloBox.cy);
                        }
                    }
                    return ret;
                });
                var line = new List<double>();
                var xmin = 1d;
                var ymin = 1d;
                var xmax = 0d;
                var ymax = 0d;
                foreach (MyBox box in node.objs)
                {
                    xmin = Math.Min(xmin, box.yoloBox.cx - box.yoloBox.width / 2);
                    ymin = Math.Min(ymin, box.yoloBox.cy - box.yoloBox.height / 2);
                    xmax = Math.Max(xmax, box.yoloBox.cx + box.yoloBox.width / 2);
                    ymax = Math.Max(ymax, box.yoloBox.cy + box.yoloBox.height / 2);
                    line.Add(box.yoloBox.cx);
                    line.Add(box.yoloBox.cy);
                }
                var w = (xmax - xmin);
                var h = (ymax - ymin);
                var cx = (xmax + xmin) / 2;
                var cy = (ymax + ymin) / 2;
                line.InsertRange(0, new List<double>() { 0, cx, cy, w, h });
                var imgFile = Path.Combine(dir, node.PhysicName);
                var txtFile = Regex.Replace(imgFile, @"\.jpeg$", ".txt").Replace("images", "labels");
                var txt = string.Join(" ", line);
                if (!Directory.Exists(Path.GetDirectoryName(txtFile)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(txtFile));
                }
                File.WriteAllText(txtFile, txt);
                File.Copy(Path.Combine(node._parent.di.FullName, node.PhysicName), imgFile, true);
                return true;
            }, node => node.IsLeaf && node.treeNode.Checked);

            return true;
        }
        public static void LoadOrInitBoxes(AnntShellEnv env, AnntNode node, (int, int) bmp)
        {
            if (node.objs == null)
            {
                if (File.Exists(node.lPath))
                {
                    LoadBoxesFromYolo(env, node, bmp);
                }
                else
                {
                    node.bmp = bmp;
                    node.objs = new List<MyBox>();
                }
            }
        }

        public static void LoadBoxesFromYolo(AnntShellEnv env, AnntNode n, (int w, int h) bmp)
        {
#if false
            var file = n._parent.di.FullName + "\\" + n._name;
            var bmp = CloneFromFile(file);
            n.bmp = (bmp.Width, bmp.Height);
            bmp.Dispose();
#endif
            n.bmp = bmp;
            n.objs = ParseTxt(env, n, n.bmp);
        }

        public static List<MyBox> ParseTxt(AnntShellEnv env, AnntNode n, (int Width, int Height) bmp)
        {
            var l_labelConfig = env.labelConfig;
            var lines = File.ReadAllLines(n.lPath);
            var objs = new List<MyBox>();
            Array.ForEach(lines, line =>
            {
                try
                {

                    var arr = line.Split();
                    if (arr.Length == 5)
                    {
                        //index x_center y_center width height
                        YoloBox yoloBox = new YoloBox()
                        {
                            cx = double.Parse(arr[1]),
                            cy = double.Parse(arr[2]),
                            width = double.Parse(arr[3]),
                            height = double.Parse(arr[4]),
                        };

                        var id = int.Parse(arr[0]);
                        var name = l_labelConfig._dict[id];
                        var obj = new MyBox { bmp = (bmp.Width, bmp.Height), yoloBox = yoloBox, LabelName = name, Id = id };
                        objs.Add(obj);
                    }
                    else
                    {
                        var points = new List<PointF>();
                        for (int i = 1; i < arr.Length; i += 2)
                        {
                            points.Add(new PointF(float.Parse(arr[i]) * bmp.Width, float.Parse(arr[i + 1]) * bmp.Height));
                        }

                        var name = l_labelConfig._dict[int.Parse(arr[0])];

                        objs.Add(new MyBox { bmp = (bmp.Width, bmp.Height), LabelName = name, Points = points });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Logger.Error(ex.Message);
                }
            });
            return objs;
        }

        public static void ConvertDictXlsxToJson(string pathXlsx, string pathJson)
        {
            var labels = ReadDictXlsx(pathXlsx);
            File.WriteAllText(pathJson, labels.ToJson());
        }

        public static List<LabelExt> ReadDictXlsx(string path, string shName = "DictModel")
        {
            var labels = new List<LabelExt>();
            var wb = new XLWorkbook(path);
            if (!wb.Worksheets.Contains(shName))
            {
                throw new Exception("Invalid excel file!");
            }
            var ws = wb.Worksheets.Worksheet(shName);
            var l_endRow = ws.LastRowUsed().RowNumber();
            var l_startRow = 2;
            const int l_idxCol = 1;
            const int l_labelCol = 2;
            const int l_scoreCol = 3;
            const int l_aliasCol = 4;
            for (int iRow = l_startRow; iRow <= l_endRow; iRow++)
            {
                try
                {
                    var value = ws.Cell(iRow, l_idxCol).GetValue<int>();
                    string name = ws.Cell(iRow, l_labelCol).GetValue<string>();
                    var score = ws.Cell(iRow, l_scoreCol).GetValue<float>();
                    string alias = ws.Cell(iRow, l_aliasCol).GetValue<string>();
                    if (string.IsNullOrEmpty(name))
                    {
                        break;
                    }

                    labels.Add(new LabelExt
                    {
                        idx = value,
                        label = name,
                        alias = alias,
                        score = score,
                    });

                }
                catch
                {
                    break;
                }
            }
            return labels;
        }

        public static (Dictionary<int, string> l_idxLabelDict, Dictionary<string, string> l_labelAliasDict) ReadDictModelXlsx(string path, string shName = "DictModel")
        {
            var l_idxLabelDict = new Dictionary<int, string>();
            var l_labelAliasDict = new Dictionary<string, string>();
            var wb = new XLWorkbook(Path.Combine(path));
            if (!wb.Worksheets.Contains(shName))
            {
                throw new Exception("Invalid excel file!");
            }
            var ws = wb.Worksheets.Worksheet(shName);
            var l_endRow = ws.LastRowUsed().RowNumber();
            var l_startRow = 2;
            const int l_idxCol = 1;
            const int l_labelCol = 2;
            const int l_aliasCol = 4;
            for (int iRow = l_startRow; iRow <= l_endRow; iRow++)
            {
                try
                {
                    var value = ws.Cell(iRow, l_idxCol).GetValue<int>();
                    string name = ws.Cell(iRow, l_labelCol).GetValue<string>();
                    string alias = ws.Cell(iRow, l_aliasCol).GetValue<string>();
                    if (string.IsNullOrEmpty(name))
                    {
                        break;
                    }

                    l_idxLabelDict.Add(value, name);

                    if (!string.IsNullOrEmpty(alias)) { l_labelAliasDict.Add(name, alias); }
                }
                catch
                {
                    break;
                }
            }

            return (l_idxLabelDict, l_labelAliasDict);
        }
    }
    public class DetectModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = RepositoryHelper.NewId();
        [JsonProperty("partNumber")]
        public string PartNumber { get; set; }
        [JsonProperty("modelFile")]
        public string ModelFileId { get; set; }
    }
}
