using annotation;
using ClosedXML.Excel;
using Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Yolov;

namespace comp
{
    public class CompModels
    {
        private Dictionary<int, string> labelDict;
        private Dictionary<string, int> labelDict2;

        public class ActualResult
        {
            public string modelName;
            public List<Box> boxes;
            public string diffJson;
            public int diff;
            public string tsName;
        }

        public class TestImg
        {
            public string path;
            public string labelFile;
            public List<Box> boxes; // expected
            public List<ActualResult> results; // model, compareResult (json)
        }
        public class Model
        {
            public string Name { get; set; }
            public Dictionary<string, TestSet> tsDict;
        }
        public class TestSet
        {
            public string Name { get; set; } // front_ok
            public List<TestImg> imgs = new List<TestImg>();
            public Dictionary<string, List<Box>> imgDict;
        }
        public class TestDir
        {
            public List<TestSet> testSets;
            public List<Model> models = new List<Model>();
            public Dictionary<int, string> labelDict;
            public string path;
        }

        private class BoxDTO : IComparable
        {
            public string label { get; set; } // "clip_rec",
            public float score { get; set; } // 0.9
            public float x { get; set; } // 10,
            public float y { get; set; } // 10,
            public float w { get; set; } // 20,
            public float h { get; set; } // 30


            public int CompareTo(object obj)
            {
                if (obj == null) return 1;
                var b = obj as BoxDTO;
                var a = this;

                int ret = a.label.CompareTo(b.label);
                if (ret == 0)
                {
                    ret = a.x.CompareTo(b.x);
                    if (ret == 0)
                    {
                        ret = a.y.CompareTo(b.y);
                    }
                }
                return ret;
            }
        }

        public (string, List<Box>) ConvBox(List<PredictionBox> lst)
        {
            List<BoxDTO> obj = ConvPredictBox(lst);
            var json = obj.ToJson();
            return (json, ConvBoxDTO(obj, labelDict2));
        }

        public static string ConvPredictBox2(List<PredictionBox> lst)
        {
            List<BoxDTO> obj = ConvPredictBox(lst);
            var json = obj.ToJson();
            return json;
        }
        private static List<BoxDTO> ConvPredictBox(List<PredictionBox> lst)
        {
            return lst.ConvertAll(b => new BoxDTO
            {
                label = b.label.name,
                score = b.score,
                x = b.rectangle.X,

                y = b.rectangle.Y,
                w = b.rectangle.Width,
                h = b.rectangle.Height,
            });
        }

        public List<Box> ParseJson(string json)
        {
            return ConvBoxDTO(json.FromJson<List<BoxDTO>>(), labelDict2);
        }

        private static List<Box> ConvBoxDTO(List<BoxDTO> lst, Dictionary<string, int> d)
        {
            if (d != null)
            {
                return lst
                    .Where(b => d.ContainsKey(b.label))
                    .OrderBy(b => b)
                    .Select(b => new Box
                    {
                        LabelName = b.label,
                        LabelId = d[b.label],
                        X = b.x,
                        Y = b.y,
                        W = b.w,
                        H = b.h,
                        Score = b.score,
                    }).ToList();
            }
            else
            {
                return lst
                    .OrderBy(b => b)
                    .Select(b => new Box
                    {
                        LabelName = b.label,
                        X = b.x,
                        Y = b.y,
                        W = b.w,
                        H = b.h,
                        Score = b.score,
                    }).ToList();
            }
        }

        public Dictionary<int, string> ParseDict(string path)
        {
            var names = File.ReadAllText(path);
            labelDict = YoloModelBase.ParseNames(names);
            labelDict2 = new Dictionary<string, int>();
            labelDict.ToList().ForEach(p => labelDict2.Add(p.Value, p.Key));
            return labelDict;
        }

        public TestDir ParseData(string dir)
        {
            var td = ParseData(dir, labelDict2);
            td.labelDict = labelDict;
            return td;
        }
        public static TestDir ParseData(string dir, Dictionary<string, int> labelDict2)
        {
            List<Model> models;
            // read test data
            var testD = new DirectoryInfo(dir + "\\test");
            var lst = new List<TestSet>();
            foreach (var sub in testD.GetDirectories())
            {
                var ts = new TestSet { Name = sub.Name };
                lst.Add(ts);
                foreach (var file in sub.GetFiles("*.jpeg"))
                {
                    var ti = CreateTestImg(file.FullName, labelDict2);
                }
            }

            // read detect data
            var modelsD = new DirectoryInfo(dir + "\\models");
            models = new List<Model>();
            foreach (var sub in modelsD.GetDirectories())
            {
                var outputD = new DirectoryInfo(Path.Combine(sub.FullName, "output"));
                var model = new Model { Name = sub.Name };
                model.tsDict = new Dictionary<string, TestSet>();
                if (!outputD.Exists) continue;

                outputD.GetDirectories().ToList().ForEach(d =>
                {
                    var tdict = new Dictionary<string, List<Box>>();
                    d.GetFiles("*.txt").ToList().ForEach(f =>
                    {
                        var boxes = ConvBoxDTO(File.ReadAllText(f.FullName).FromJson<List<BoxDTO>>(), labelDict2);
                        tdict.Add(Path.GetFileNameWithoutExtension(f.Name), boxes);
                    });

                    model.tsDict.Add(d.Name, new TestSet { imgDict = tdict });
                });
                models.Add(model);
            }

            var td = new TestDir { path = dir, testSets = lst, models = models };
            return td;
        }
        private List<(string, int)> CompBoxes(List<Box> expected, List<Box> acttual)
        {
            var d = new Dictionary<string, Box>();
            acttual.ForEach(p =>
            {
                if (!Regex.IsMatch(p.label.name, "^ng_"))
                {
                    d.Add(p.label.name, p);
                }
            });

            var lst = expected.ConvertAll(p =>
            {
                if (d.ContainsKey(p.label.name))
                {
                    // compare
                    var n = CompBox(p, d[p.label.name]);
                    if (n != 0)
                    {
                        Logger.Logger.Debug(JsonConvert.SerializeObject(new
                        {
                            expect = p,
                            actual = d[p.label.name]
                        }));
                    }
                    d.Remove(p.label.name);
                    return (p.label.name, n);
                }
                return (p.label.name, 1); // obj missing
            });


            return lst;
        }

        private int CompBox(Box a, Box b)
        {
            return a.CompareTo(b);
        }


        public static (int, string) Compbox(string tsName, string modelName, TestImg img, List<Box> boxes2)
        {
            var (d, diffJson) = CompBox(img.boxes, boxes2);
            img.results.Add(new ActualResult
            {
                boxes = boxes2,
                modelName = modelName,
                tsName = tsName,
                diff = d,
                diffJson = diffJson,
            });
            return (d, diffJson);
        }

        private static (int d, string diffJson) CompBox(List<Box> boxes, List<Box> boxes2)
        {
            var (d, l) = CompExtensions.Diff(boxes, boxes2);
            var diffJson = l.Where(i => i.@type != CompExtensions.EditType.none).ToList().ToJson();
            return (d, diffJson);
        }

        public static void CompAndExport(string dir, CompModels.TestDir td)
        {
            if (td == null) { return; }
            var lines = new List<List<string>>();
            var firstLine = new List<string> { "test set", "#", "file" };
            firstLine.AddRange(td.models.ConvertAll(m => m.Name));
            lines.Add(firstLine);

            td.testSets.ForEach(ts =>
            {
                lines.Add(new List<string> { ts.Name });
                lines.AddRange(ts.imgs.Zip(Enumerable.Range(1, ts.imgs.Count), (img, i) =>
                {
                    var line = new List<string>
                    {
                        "",
                        $"{i}",
                        img.path
                    };
                    var key = Path.GetFileNameWithoutExtension(img.path);
                    img.results = td.models.ConvertAll(model =>
                    {
                        if (model.tsDict.ContainsKey(ts.Name))
                        {
                            var mts = model.tsDict[ts.Name];
                            if (mts.imgDict.ContainsKey(key))
                            {
                                var boxes2 = mts.imgDict[key].Where(p => !Regex.IsMatch(p.label.name, "^ng_")).ToList();
                                var (d, json) = CompBox(img.boxes, boxes2);

                                if (d == 0)
                                {
                                    line.Add("OK");
                                }
                                else
                                {
                                    line.Add("NG\n" + json);
                                }
                                return new ActualResult
                                {
                                    boxes = boxes2,
                                    modelName = model.Name,
                                    tsName = ts.Name,
                                    diff = d,
                                    diffJson = json
                                };
                            }
                            else
                            {
                                line.Add("err2");
                            }
                        }
                        else
                        {
                            line.Add("err1");
                        }
                        return null;
                    }).Where(obj => obj != null).ToList();
                    return line;
                }).ToList());
            });

            // File.WriteAllText(Path.Combine(dir,"log.csv"), string.Join("\r\n",lines));
            var outxlsx = Path.Combine(Path.Combine(dir, "log.xlsx"));
            ExportToXlsx(lines, outxlsx);
        }

        private static void ExportToXlsx(List<List<string>> lines, string outxlsx)
        {
            var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Sheet1");
            var iRow = 1;
            lines.ForEach(line =>
            {
                var iCol = 1;
                line.ForEach(v =>
                {
                    ws.Cell(iRow, iCol).Value = v;
                    iCol++;
                });
                iRow++;
            });
            wb.SaveAs(outxlsx);
        }

        public TestImg CreateTestImg(string path)
        {
            var img = CreateTestImg(path, labelDict2);
            return img;
        }

        // check file & load boxes by order
        // ↓ json.txt
        // ↓ FR.xml
        // ↓ yolo.txt
        public static TestImg CreateTestImg(string path, Dictionary<string, int> labelIdDict)
        {
            var ti = new TestImg { path = path };
            ti.results = new List<ActualResult> { };

            //var textFile = Regex.Replace(path, "jpeg$", "txt");
            //if (File.Exists(textFile))
            //{
            //    ti.boxes = ConvBox(File.ReadAllText(textFile).FromJson<List<BoxDTO>>(), labelIdDict);
            //    ti.labelFile = textFile;
            //    return ti;
            //}

            //var xmlFile = Regex.Replace(path, "jpeg$", "xml");
            //if (File.Exists(xmlFile))
            //{
            //    var fr = FRNotation.ParseXml(xmlFile);
            //    ti.boxes = ConvBox(fr.boxes.ConvertAll(b => new BoxDTO
            //    {
            //        x = b.bndbox.xmin,
            //        y = b.bndbox.ymin,
            //        w = b.bndbox.xmax - b.bndbox.xmin,
            //        h = b.bndbox.ymax - b.bndbox.ymin,
            //        label = b.label
            //    }), labelIdDict);
            //    ti.labelFile = xmlFile;
            //    return ti;
            //}

            var labelD = Path.GetDirectoryName(path).ReplaceLastOccurrence("images", "labels");
            var yoloFile = Path.Combine(labelD, Path.GetFileNameWithoutExtension(path) + ".txt");
            if (File.Exists(yoloFile))
            {
                var lines = File.ReadAllLines(yoloFile);
                var bmp = ImageExtensions.CloneFromFile(path);
                var objs = lines.ToList().ConvertAll(line =>
                {
                    var arr = line.Split();
                    var id = int.Parse(arr[0]);
                    var name = labelIdDict.Keys.ElementAt(id);
                    if (arr.Length == 5)
                    {
                        // box
                        var x_center = float.Parse(arr[1]) * bmp.Width;
                        var y_center = float.Parse(arr[2]) * bmp.Height;
                        var width = float.Parse(arr[3]) * bmp.Width;
                        var height = float.Parse(arr[4]) * bmp.Height;
                        var xmin = x_center - width / 2;
                        var ymin = y_center - height / 2;

                        return new BoxDTO { x = xmin, y = ymin, w = width, h = height, label = name, score = 0 };
                    }
                    else
                    {
                        // segment
                        var points = new List<PointF>();
                        float xmin = bmp.Width, xmax = 0, ymin = bmp.Height, ymax = 0;
                        for (int i = 1; i < arr.Length; i += 2)
                        {
                            var x = float.Parse(arr[i]) * bmp.Width;
                            var y = float.Parse(arr[i + 1]) * bmp.Height;
                            var point = new PointF(x, y);
                            points.Add(point);
                            xmin = Math.Min(xmin, x);
                            xmax = Math.Max(xmax, x);
                            ymin = Math.Min(ymin, y);
                            ymax = Math.Max(ymax, y);
                        }

                        return new BoxDTO { x = xmin, y = ymin, w = xmax - xmin, h = ymax - ymin, label = name, score = 0 };
                    }
                });
                bmp.Dispose();
                ti.boxes = ConvBoxDTO(objs, labelIdDict);
                ti.labelFile = yoloFile;
                return ti;
            }

            return ti;
        }

        public static void SaveAsYolo(TestImg ti)
        {
            var img = ImageExtensions.CloneFromFile(ti.path);
            var bmp = (img.Width, img.Height);
            img.Dispose();
            var lines = ti.boxes.ConvertAll(box =>
            {
                var w = (double)box.W / bmp.Width;
                var h = (double)box.H / bmp.Height;
                var cx = ((double)box.X + ((double)box.W) / 2) / bmp.Width;
                var cy = ((double)box.Y + ((double)box.H) / 2) / bmp.Height;
                var line = $"{box.LabelId} {cx} {cy} {w} {h}";
                return line;
            });
            var labelD = Path.GetDirectoryName(ti.path).ReplaceLastOccurrence("images", "labels");
            var yoloFile = Path.Combine(labelD, Path.GetFileNameWithoutExtension(ti.path) + ".txt");
            Debug.Assert(File.Exists(yoloFile));
            File.WriteAllLines(yoloFile, lines);
        }
    }
}
