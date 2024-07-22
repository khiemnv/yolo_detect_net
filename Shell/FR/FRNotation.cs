using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using static FR.FRNotation.FRBoxesCollection;

namespace FR
{
    public class FRNotation
    {
        public class FRBoxesCollection
        {
            public class Size
            {
                public int height { get; set; }
                public int width { get; set; }
            }
            public Size size;
            public string segmented;

            public class FRBox
            {
                public string label => name;
                public string name { get; set; }
                public class Bndbox
                {
                    public int xmin { get; set; }
                    public int ymin { get; set; }
                    public int xmax { get; set; }
                    public int ymax { get; set; }
                }

                public Bndbox bndbox { get; set; }
            }
            public List<FRBox> boxes { get; set; }
        }

        public static string SerializeObject<T>(T toSerialize)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(toSerialize.GetType());

            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, toSerialize);
                return textWriter.ToString();
            }
        }
        public static T DeSerializeObject<T>(string txt)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));

            var tmpl = $"<{typeof(T).Name}>{txt}</{typeof(T).Name}>";
            using (TextReader reader = new StringReader(tmpl))
            {
                var result = (T)xmlSerializer.Deserialize(reader);
                return result;
            }
        }

        public static List<string> ConvXml2Yolo(string path, Dictionary<string, int> dict)
        {
            var fr = ParseXml(path);
            var (imageWidth, imageHeight) = (fr.size.width, fr.size.height);
            List<FRBox> objs = fr.boxes;

            return objs.ConvertAll(shape =>
            {
                if (!dict.ContainsKey(shape.label)) { dict.Add(shape.label, dict.Count); }
                var idx = dict[shape.label];
                var (x, y, dx, dy) = (
                    (double)shape.bndbox.xmin,
                    (double)shape.bndbox.ymin,
                    (double)shape.bndbox.xmax - shape.bndbox.xmin,
                    (double)shape.bndbox.ymax - shape.bndbox.ymin);
                var points = new List<double[]> {
                    new double[] {(x+dx/2)/imageWidth,(y+dy/2)/imageHeight },
                    new double[] {dx/imageWidth,dy/imageHeight },
                };
                var line = $"{idx} " + string.Join(" ", points.ConvertAll(p => $"{p[0]} {p[1]}"));
                return line;
            });
        }

        public static FRBoxesCollection ParseXml(string path)
        {
            var xmlSerializer = new XmlSerializer(typeof(FRBox));
            var doc = new XmlDocument();
            doc.LoadXml(File.ReadAllText(path));


            var sizes = doc.GetElementsByTagName("size");
            var size = DeSerializeObject<Size>(sizes[0].InnerXml);

            var elemList = doc.GetElementsByTagName("object");
            var objs = new List<FRBox>();
            foreach (XmlNode e in elemList)
            {
                var txt = e.InnerXml;
                var obj = DeSerializeObject<FRBox>(txt);
                objs.Add(obj);
            }

            var segmenteds = doc.GetElementsByTagName("segmented");
            var segmented = segmenteds[0].InnerXml;

            return new FRBoxesCollection { size = size, boxes = objs, segmented = segmented };
        }

        public static List<string> ParsePbtxt(string path)
        {
            // parse
            var txt = System.IO.File.ReadAllText(path);
            var m = Regex.Matches(txt, @"item {[\s\r\n]+id: (\d+)[\s\r\n]+name: '(\w+)'[\s\r\n]+}", RegexOptions.Multiline);
            var lst = new List<(int, string)>();
            foreach (Match i in m)
            {
                lst.Add((int.Parse(i.Groups[1].Value), i.Groups[2].Value));
            }

            var names = string.Join("\n", lst.ConvertAll(i => "  " + (i.Item1 - 1) + ": " + i.Item2));
            return lst.ConvertAll(i => i.Item2);
        }
    }

}
