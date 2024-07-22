using Extensions;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LabelMe
{
    public class Labelme
    {
        public class LM_Shape
        {
            public string label { get; set; }
            public List<double[]> points { get; set; }
        }

        public List<LM_Shape> shapes { get; set; }
        public int imageHeight { get; set; }
        public int imageWidth { get; set; }


        public static List<string> ConvLabelMe2Yolo(string fileJson, IDictionary<string, int> dict)
        {
            var txt = File.ReadAllText(fileJson);
            Labelme data = File.ReadAllText(fileJson).FromJson<Labelme>();
            var lines = data.shapes.ConvertAll(shape =>
            {
                if (!dict.ContainsKey(shape.label)) { dict.Add(shape.label, dict.Count); }
                var idx = dict[shape.label];
                var line = $"{idx} " + string.Join(" ", shape.points.ConvertAll(p => $"{p[0] / data.imageWidth} {p[1] / data.imageHeight}"));
                return line;
            });
            return lines;
        }

        public static string ConvYolo2Labelme(string fileYolo, (int Width, int Height) image, IDictionary<int, string> dict2)
        {
            var lines = File.ReadAllLines(fileYolo);
            var objs = lines.ToList().ConvertAll(line =>
            {
                var arr = line.Split();
                if (arr.Length == 5)
                {
                    //index x_center y_center width height
                    var (cx, cy, w, h) = (double.Parse(arr[1]) * image.Width,
                                double.Parse(arr[2]) * image.Height,
                                double.Parse(arr[3]) * image.Width,
                                double.Parse(arr[4]) * image.Height);

                    var id = int.Parse(arr[0]);
                    var name = dict2[id];
                    var shape = new LM_Shape()
                    {
                        points = new List<double[]>
                        {
                            new double[]{cx,cy},
                            new double[]{w,h},
                        },
                        label = name,
                    };
                    return shape;
                }
                else
                {
                    var points = new List<double[]>();
                    for (int i = 1; i < arr.Length; i += 2)
                    {
                        points.Add(new double[] { double.Parse(arr[i]) * image.Width, double.Parse(arr[i + 1]) * image.Height });
                    }

                    var name = dict2[int.Parse(arr[0])];

                    return new LM_Shape { label = name, points = points };
                }

            });
            return new Labelme { imageHeight = image.Height, imageWidth = image.Width, shapes = objs }.ToJson();
        }
    }
}
