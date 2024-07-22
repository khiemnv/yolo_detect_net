using FR;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Yolov;

namespace annotation
{
    internal static class YolovExtension
    {

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

        // model, path, type
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
                        string type = YolovExtension.GetModelType(sub);
                        lst.Add((sub.Name, f.FullName, type));
                    }
                }
            }
            return lst;
        }
        public static string ReplaceLastOccurrence(this string source, string find, string replace)
        {
            int place = source.LastIndexOf(find);

            if (place == -1)
                return source;

            return source.Remove(place, find.Length).Insert(place, replace);
        }
    }
}