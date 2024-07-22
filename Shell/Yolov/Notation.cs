using Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Yolov
{
    public class Notation
    {
        private class Myitem
        {
            public string groupName;
            public string img;
            public string key;
            public string notation;
        }

        public static Dictionary<int, string> ReadLabelDict(string path)
        {
            try
            {
                var dict = new Dictionary<int, string>();
                var unique = new Dictionary<string, int>();
                var lines = File.ReadAllLines(path);
                var s = 0;
                lines.ToList().ForEach(line =>
                {
                    switch (s)
                    {
                        case 0:
                            if (Regex.IsMatch(line, "names:"))
                            {
                                s = 1;
                            }
                            break;
                        case 1:
                            var arr = line.Split(new char[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                            var idx = int.Parse(arr[0]);
                            var name = arr[1];
                            dict.Add(idx, name);
                            unique.Add(name, idx);
                            break;
                    }
                });

                return dict;
            }
            catch (Exception ex)
            {
                Logger.Logger.Error(ex.Message);
                return null;
            }
        }

        private static void RemoveEmptyFolderR(string startLocation)
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                RemoveEmptyFolderR(directory);
                if (Directory.GetFiles(directory).Length == 0 &&
                    Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory, false);
                }
            }
        }
        public static void ValidFolder(string inDir)
        {
            try
            {
                var dict = new Dictionary<string, Myitem>();
                var d = new DirectoryInfo(Path.Combine(inDir, "images"));
                if (!d.Exists) return;

                // clean empty dir
                RemoveEmptyFolderR(inDir);

                var duplicate = new List<string>();
                var lst = Array.ConvertAll(d.GetDirectories(), s =>
                {
                    Array.ForEach(s.GetFiles("*.jpeg"), f =>
                    {
                        var key = Path.GetFileNameWithoutExtension(f.Name);
                        if (dict.ContainsKey(key)) { duplicate.Add(f.FullName); }
                        else
                        {
                            dict.Add(key, new Myitem
                            {
                                groupName = s.Name,
                                img = f.FullName,
                                key = key,
                            });
                        }
                    });
                    return s.Name;
                });
                Logger.Logger.Debug(duplicate.ToJson());
                duplicate.ForEach(path => dict.Remove(Path.GetFileNameWithoutExtension(path)));

                var l = new DirectoryInfo(inDir + "\\labels");
                if (!l.Exists) return;
                var labels = new List<Myitem>();
                var moved = 0;
                Array.ForEach(l.GetDirectories(), s =>
                {
                    Array.ForEach(s.GetFiles("*.txt"), f =>
                    {
                        var key = Path.GetFileNameWithoutExtension(f.Name);
                        if (dict.ContainsKey(key))
                        {
                            var obj = dict[key];
                            if (obj.groupName != s.Name)
                            {
                                var groupD = Path.Combine(l.FullName, obj.groupName);
                                if (!Directory.Exists(groupD))
                                {
                                    Directory.CreateDirectory(groupD);
                                }
                                var desFile = Path.Combine(groupD, f.Name);
                                if (File.Exists(desFile))
                                {
                                    f.Delete();
                                }
                                else
                                {
                                    f.MoveTo(desFile);
                                }
                                moved++;
                            }
                        }
                        else
                        {
                            labels.Add(new Myitem { notation = f.FullName });
                        }
                    });
                });

                // update dataset.yaml
                var trains = new List<string>();
                var vals = new List<string>();
                var tests = new List<string>();
                foreach (var key in lst)
                {
                    if (Regex.IsMatch(key, "^val($|_.*)"))
                    {
                        vals.Add(key);
                    }
                    else if (Regex.IsMatch(key, "^test($|_.*)")) { tests.Add(key); }
                    else { trains.Add(key); }
                }
                UpdateCfgFile(inDir, trains, vals, tests);
            }
            catch (Exception ex)
            {
                Logger.Logger.Error(ex.Message);
            }
        }

        private static void UpdateCfgFile(string inDir,
            List<string> trains,
            List<string> vals,
            List<string> tests = null,
            List<string> labels = null)
        {
            var path = inDir.Replace("\\", "/");
            var cfgFile = Path.Combine(inDir, "dataset.yaml");

            if (labels == null)
            {
                var labeld = ReadLabelDict(cfgFile);
                if (labeld == null) { throw new Exception("Invalid config!"); }
                labels = labeld.Values.ToList();
            }

            var ztrain = string.Join("\r\n", trains.Select(sub => "  - images/" + sub).ToList());
            var zval = string.Join("\r\n", vals.Select(sub => "  - images/" + sub).ToList());
            var ztest = string.Join("\r\n", tests.Select(sub => "  - images/" + sub).ToList());
            var names = labels.Zip(Enumerable.Range(0, labels.Count), (value, key) => $"  {key}: {value}").ToList();
            var lines = new List<string> {
                $"path: {path}",
                $"train:",
                $"{ztrain}",
                $"val:",
                $"{zval}",
                $"test: ",
                $"{ztest}",
                $"names:",
                string.Join("\r\n", names)
            };
            File.WriteAllLines(cfgFile, lines);
        }

        public static void ValidName(string inDir)
        {
            var d = new DirectoryInfo(inDir + "\\images");
            var groups = new Dictionary<string, Dictionary<string, Myitem>>();
            Array.ForEach(d.GetDirectories(), s =>
            {
                var l = new DirectoryInfo(inDir + "\\labels\\" + s.Name);
                if (!l.Exists) return;

                var groupName = Regex.Replace(s.Name, "(train|val)_", "");
                if (!groups.ContainsKey(groupName))
                    groups.Add(groupName, new Dictionary<string, Myitem>());

                var dict = groups[groupName];
                Array.ForEach(s.GetFiles(), f =>
                {
                    var key = Path.GetFileNameWithoutExtension(f.Name);
                    dict.Add(key, new Myitem
                    {
                        groupName = groupName,
                        img = f.FullName,
                        key = key,
                    }); ;
                });

                Array.ForEach(l.GetFiles(), f =>
                {
                    var key = Path.GetFileNameWithoutExtension(f.Name);
                    if (dict.ContainsKey(key))
                    {
                        dict[key].notation = f.FullName;
                    }
                });

            });

            var lst = new List<(string, Myitem)>();
            foreach (var g in groups)
            {
                var dict = g.Value;
                var groupName = g.Key;
                var nameLst = new List<string>();
                for (int i = 1; i <= dict.Count; i++)
                {
                    var newName = $"{groupName}_{i:D3}";
                    if (!dict.ContainsKey(newName))
                    {
                        nameLst.Add(newName);

                    }
                    else
                    {
                        dict.Remove(newName);
                    }
                }

                lst.AddRange(nameLst.Zip(dict.Values.ToList(), (newName, item) =>
                {
                    // rename
                    var newimgname = Path.Combine(Path.GetDirectoryName(item.img), newName + Path.GetExtension(item.img));
                    File.Move(item.img, newimgname);
                    var newnotename = Path.Combine(Path.GetDirectoryName(item.notation), newName + Path.GetExtension(item.notation));
                    File.Move(item.notation, newnotename);
                    return (newName, item);
                }).ToList());
            }
        }
    }
}
