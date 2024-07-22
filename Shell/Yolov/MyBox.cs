using comp;
using Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace annotation
{
    public class MyBox : IBox
    {
        public int Id { get; set; }
        public string LabelName { get; set; }
        public int percent;
        public double clsConfidence;
        public Double Xmin { get => Rect.X; }
        public Double Ymin { get => Rect.Y; }
        public Double Xmax { get => Xmin + Width; }
        public Double Ymax { get => Ymin + Height; }
        //public Vector2D CenterPos;
        public int Width { get => Rect.Width; }
        public int Height { get => Rect.Height; }
        private System.Drawing.Rectangle? rect;
        public IEnumerable<System.Drawing.PointF> points;
        public IEnumerable<System.Drawing.PointF> Points
        {
            get => points; set
            {
                points = value;
                rect = null;
            }
        }
        public bool IsRect => points == null;

        public float X { get => (float)(yoloBox.cx - yoloBox.width / 2) * bmp.Width; }
        public float Y { get => (float)(yoloBox.cy - yoloBox.height / 2) * bmp.Height; }
        public float W { get => (float)yoloBox.width * bmp.Width; }
        public float H { get => (float)yoloBox.height * bmp.Height; }

        public (int Width, int Height) bmp;
        public int BmpW => bmp.Width;
        public int BmpH => bmp.Height;

        public YoloBox yoloBox;
        public System.Drawing.Rectangle Rect
        {
            get
            {
                if (IsRect)
                    return BoxExtensions.FromYoloBox(yoloBox, BmpW, BmpH);
                else if (rect == null)
                {
                    rect = BoxExtensions.FromPoints(Points);
                    return rect.Value;
                }
                else
                    return rect.Value;
            }
            set => yoloBox = BoxExtensions.ToYoloBox(value, BmpW, BmpH);
        }

        public MyBox() { }

        public string FormatAsYolo()
        {
            var obj = this;
            var (w, h) = (obj.bmp.Width, obj.bmp.Height);
            var box = obj.yoloBox;
            var idx = Id;
            var line = $"{idx} {box.cx} {box.cy} {box.width} {box.height}";
            if (!obj.IsRect)
            {
                line = $"{idx} {string.Join(" ", obj.Points.Select(p => $"{p.X / w} {p.Y / h}"))}";
            }
            return line;
        }

        public bool deleted;
        public bool changed;
        public string identify = RepositoryHelper.NewId();
    }

    public class Parser
    {
        public static string ExportXml<T>(string path, IEnumerable<T> boxes, string segmented = "0")
            where T : IBox
        {
            string folder = "";
            string filename = "";
            if (!string.IsNullOrEmpty(path))
            {
                filename = Path.GetFileName(path);
                folder = Path.GetFileName(Path.GetDirectoryName(path));
            }
            return ExportXml<T>(folder, filename, path, boxes, segmented);
        }

        public static string ExportXml<T>(string folder, string filename, string path, IEnumerable<T> boxes, string segmented = "0")
            where T : IBox
        {
            XmlDocument xmlDoc = new XmlDocument();
            // create root element and add it to the document
            XmlElement rootElement = xmlDoc.CreateElement("annotation");
            xmlDoc.AppendChild(rootElement);
            // add child elements to root element
            XmlElement folderElement = xmlDoc.CreateElement("folder");
            folderElement.InnerText = folder;
            rootElement.AppendChild(folderElement);

            XmlElement filenameElement = xmlDoc.CreateElement("filename");
            filenameElement.InnerText = filename;
            rootElement.AppendChild(filenameElement);

            XmlElement pathElement = xmlDoc.CreateElement("path");
            pathElement.InnerText = path;
            rootElement.AppendChild(pathElement);

            XmlElement sourceElement = xmlDoc.CreateElement("source");
            rootElement.AppendChild(sourceElement);

            XmlElement databaseElement = xmlDoc.CreateElement("database");
            databaseElement.InnerText = "Unknown";
            sourceElement.AppendChild(databaseElement);

            XmlElement sizeElement = xmlDoc.CreateElement("size");
            rootElement.AppendChild(sizeElement);

            XmlElement widthElement = xmlDoc.CreateElement("width");
            widthElement.InnerText = "4656";
            sizeElement.AppendChild(widthElement);

            XmlElement heightElement = xmlDoc.CreateElement("height");
            heightElement.InnerText = "3496";
            sizeElement.AppendChild(heightElement);

            XmlElement depthElement = xmlDoc.CreateElement("depth");
            depthElement.InnerText = "3";
            sizeElement.AppendChild(depthElement);

            XmlElement segmentedElement = xmlDoc.CreateElement("segmented");
            segmentedElement.InnerText = segmented;
            rootElement.AppendChild(segmentedElement);
            /***********************************************************************/
            foreach (var obj in boxes)
            {
                XmlElement objElement = xmlDoc.CreateElement("object");
                rootElement.AppendChild(objElement);

                XmlElement nameElement = xmlDoc.CreateElement("name");
                nameElement.InnerText = obj.LabelName;
                objElement.AppendChild(nameElement);

                XmlElement poseElement = xmlDoc.CreateElement("pose");
                poseElement.InnerText = "Unspecified";
                objElement.AppendChild(poseElement);

                XmlElement truncatedElement = xmlDoc.CreateElement("truncated");
                truncatedElement.InnerText = "0";
                objElement.AppendChild(truncatedElement);

                XmlElement difficultElement = xmlDoc.CreateElement("difficult");
                difficultElement.InnerText = "0";
                objElement.AppendChild(difficultElement);

                XmlElement bndboxElement = xmlDoc.CreateElement("bndbox");
                objElement.AppendChild(bndboxElement);

                XmlElement xminElement = xmlDoc.CreateElement("xmin");
                xminElement.InnerText = ((int)obj.X).ToString();
                bndboxElement.AppendChild(xminElement);

                XmlElement yminElement = xmlDoc.CreateElement("ymin");
                yminElement.InnerText = ((int)obj.Y).ToString();
                bndboxElement.AppendChild(yminElement);

                XmlElement xmaxElement = xmlDoc.CreateElement("xmax");
                xmaxElement.InnerText = ((int)(obj.X + obj.W)).ToString();
                bndboxElement.AppendChild(xmaxElement);

                XmlElement ymaxElement = xmlDoc.CreateElement("ymax");
                ymaxElement.InnerText = ((int)(obj.Y + obj.H)).ToString();
                bndboxElement.AppendChild(ymaxElement);

            }
            // tao dau tab dau dong cho tat ca cac element xml
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.Indent = true;
            settings.IndentChars = "\t";
            settings.Encoding = System.Text.Encoding.UTF8;


            using (var sw = new StringWriter())
            {
                using (var xw = XmlWriter.Create(sw, settings))
                {
                    // Build Xml with xw.
                    xmlDoc.WriteTo(xw);
                }
                return sw.ToString();
            }
        }
    }
}
