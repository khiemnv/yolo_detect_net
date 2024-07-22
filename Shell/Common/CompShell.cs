using ClosedXML.Excel;
using comp;
using FR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Yolov;
using static annotation.BgWrk;
using static comp.CompModels;

namespace annotation
{
    public class CompShell
    {

        public static CompNode Explore(CompShellEnv env, ReportCallback cb = null)
        {
            var rootd = new DirectoryInfo(env._wkDir.test);

            var dictFile = env._wkDir.names_txt;
            if (!File.Exists(dictFile)) { return null; }

            var names = File.ReadAllText(dictFile);
            var labelDict = YoloModelBase.ParseNames(names);
            var labelDict2 = new Dictionary<string, int>();
            labelDict.ToList().ForEach(p => labelDict2.Add(p.Value, p.Key));

            var rootNode = new CompNode(rootd);
            cb?.Invoke(rootNode);
            rootNode._nodes = rootd.GetDirectories().ToList().ConvertAll(subd =>
            {
                var subNode = new CompNode(subd) { _parent = rootNode };
                cb?.Invoke(subNode);
                //Logger.Logger.Debug(Environment.CurrentManagedThreadId + "s:" + subNode._path);
                subNode._nodes = subd.GetFiles("*.jpeg").ToList().ConvertAll(file =>
                {
                    var imgNode = new CompNode(file)
                    {
                        _parent = subNode,
                        //testImg = CreateTestImg(file.FullName)
                    };

                    cb?.Invoke(imgNode);
                    return imgNode;
                });
                return subNode;
            });

            return rootNode;
        }

        public static bool Detect(CompShellEnv env, CompNode rootNode, string modelPath, ReportCallback cb = null)
        {
            WorkingDir wkDir = env._wkDir;
            var device = env._deviceDetect;


            var modelDir = Path.GetDirectoryName(modelPath);
            var modelName = Path.GetFileName(modelDir);
            var outdir = Path.Combine(modelDir, "output");
            if (!Directory.Exists(outdir)) { Directory.CreateDirectory(outdir); }

            // load detect result .txt
            var dict = new Dictionary<string, string>();
            var stack = new List<DirectoryInfo> { new DirectoryInfo(outdir) };
            while (stack.Count > 0)
            {
                var cur = stack[0];
                stack.RemoveAt(0);
                foreach (var file in cur.GetFiles("*.txt"))
                {
                    var key = Path.GetFileNameWithoutExtension(file.Name);
                    dict.Add(key, file.FullName);
                }
                stack.InsertRange(0, cur.GetDirectories());
            }

            // init list of not detected img
            var lst = new List<CompNode>();

            // dfs
            var bLoaded = false;
            env._traverser.Start(rootNode, (n) =>
            {
                try
                {
                    Detect(n);
                    cb?.Invoke(n);
                }
                catch
                {
                    // detect error
                }

                return true;
            }, node => node.isFile);

            if (lst.Count == 0) { return true; }

            // load detect model
            LoadModel();

            // detect
            foreach (var n in lst)
            {
                var ts = n._parent;
                var labelDir = Path.Combine(outdir, ts.di.Name);
                if (!Directory.Exists(labelDir)) { Directory.CreateDirectory(labelDir); }
                var imgDir = ts.di.FullName;
                var imgPath = Path.Combine(imgDir, n._path);
                var key = Path.GetFileNameWithoutExtension(imgPath);
                var jsonFile = Path.Combine(labelDir, key + ".txt");

                var pbc = env.yolo.Detect(imgPath, null);
                var json = ConvPredictBox2(pbc.boxes);
                File.WriteAllText(jsonFile, json);

                cb?.Invoke(n);
            }

            return true;

            void LoadModel()
            {
                var p = new DirectoryInfo(Path.GetDirectoryName(modelPath));

                if (env.yolo == null || modelPath != env.yoloModelPath)
                {
                    string type = YolovExtension.GetModelType(p);
                    switch (type)
                    {
                        case nameof(FasterRetinanet):
                            env.yolo = new FasterRetinanetCV
                            {
                                Overlap = 0.45f,
                                ModelConfidence = 0.6f,
                                DeviceDetect = device,
                            };
                            break;
                        case nameof(YoloModel8):
                            env.yolo = new YoloModel8
                            {
                                Overlap = 0.45f,
                                ModelConfidence = 0.6f,
                                DeviceDetect = device,
                            };
                            break;
                        case nameof(VinoYolo8):
                            env.yolo = new VinoYolo8
                            {
                                Overlap = 0.45f,
                                ModelConfidence = 0.6f,
                                DeviceDetect = device,
                            };
                            break;
                        default:
                            throw new Exception("Invalid model type");
                    }

                    env.yoloModelPath = modelPath;
                    env.yolo.Detect(null, modelPath);
                }
            }
            void Detect(CompNode n)
            {
                var ts = n._parent;
                var labelDir = Path.Combine(outdir, ts.di.Name);
                if (!Directory.Exists(labelDir)) { Directory.CreateDirectory(labelDir); }

                var imgDir = ts.di.FullName;
                var imgPath = Path.Combine(imgDir, n._path);
                var key = Path.GetFileNameWithoutExtension(imgPath);
                var jsonFile = Path.Combine(labelDir, key + ".txt");
                if (File.Exists(jsonFile))
                {
                    return;
                }

                if (dict.ContainsKey(key))
                {
                    File.Move(dict[key], jsonFile);
                    return;
                }

                //lst.Add(n);
                if (!bLoaded)
                {
                    LoadModel();
                    bLoaded = true;
                }

                var pbc = env.yolo.Detect(imgPath, null);
                var json = ConvPredictBox2(pbc.boxes);
                File.WriteAllText(jsonFile, json);
            }
        }

        public static void CompareNode(CompShellEnv env, CompNode n, string imgPath, string jsonFile, string modelName, string tsName)
        {
            WorkingDir wkDir = env._wkDir;
            var cmp = env._cmp;

            var imgDir = n._parent.di.FullName;
            var labelDir = Path.Combine(wkDir.models, "output", n._parent.di.Name);
            if (n.testImg == null)
            {
                n.testImg = cmp.CreateTestImg(imgPath);
            }
            else
            {
                imgPath = n.testImg.path;
            }
            if (n.testImg.boxes == null) { return; }

            var img = n.testImg;
            if (img.results == null) { img.results = new List<ActualResult> { }; }

            if (img.results.FirstOrDefault(item => item.modelName == modelName) != null)
            {
                return;
            }

            List<Box> boxes2;
            if (File.Exists(jsonFile))
            {
                boxes2 = cmp.ParseJson(File.ReadAllText(jsonFile));
            }
            else
            {
                //var pbc = yolo.Detect(imgPath, null);
                //var (json, lst2) = cmp.ConvBox(pbc.boxes);

                //File.WriteAllText(jsonFile, json);

                //boxes2 = lst2;
                throw new Exception("file json.txt not exist");
            }

            // compare
            var (d, diffJson) = Compbox(tsName, modelName, img, boxes2);
            if (d != 0)
            {
                Logger.Logger.Error(imgPath + diffJson);
            }
        }


        public static string ExportCompResult(CompShellEnv env, List<CompNode> lst)
        {
            var wkDir = env._wkDir;
            var dt = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var xlsx = Path.Combine(wkDir.path, $"{dt}_cmp.xlsx");
            var modelD = new Dictionary<string, int>();
            var cells = new List<(int row, int col, string val)>();
            var row = 2;
            var col = 1;
            // 1st row [      ,     , modle          ]
            //   n row [tsname, file, compare result,]
            //            1       2     3

            cells.Add((1, 2, "img"));

            foreach (var ts in lst)
            {
                cells.Add((row, 1, ts.PhysicName));
                row++;
                foreach (var node in ts._nodes)
                {
                    var img = node.testImg;
                    if (img == null) continue;
                    if (img.results == null) continue;

                    cells.Add((row, 2, node.PhysicName));

                    foreach (var item in img.results)
                    {
                        if (!modelD.ContainsKey(item.modelName))
                        {
                            modelD.Add(item.modelName, modelD.Count);
                        }

                        var iCol = 3 + modelD[item.modelName];

                        if (item.diff == 0)
                        {
                            cells.Add((row, iCol, "OK"));
                        }
                        else
                        {
                            cells.Add((row, iCol, "NG\n" + item.diffJson));
                        }
                    }
                    row++;
                }
            }

            foreach (var p in modelD)
            {
                cells.Add((1, 3 + p.Value, p.Key));
            }

            var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Sheet1");
            foreach (var (iRow, iCol, v) in cells)
            {
                ws.Cell(iRow, iCol).Value = v;
            }
            wb.SaveAs(xlsx);
            return xlsx;
        }
    }
}
