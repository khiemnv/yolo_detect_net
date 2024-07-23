using Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Yolov;
using Panel = Models.Panel;

namespace WP_GUI
{
    public class Parser
    {
        public static string ExportXml(string folder, string filename, string path, List<Box> boxes)
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
            segmentedElement.InnerText = "0";
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
                xminElement.InnerText = obj.X.ToString();
                bndboxElement.AppendChild(xminElement);

                XmlElement yminElement = xmlDoc.CreateElement("ymin");
                yminElement.InnerText = obj.Y.ToString();
                bndboxElement.AppendChild(yminElement);

                XmlElement xmaxElement = xmlDoc.CreateElement("xmax");
                xmaxElement.InnerText = (obj.X + obj.W).ToString();
                bndboxElement.AppendChild(xmaxElement);

                XmlElement ymaxElement = xmlDoc.CreateElement("ymax");
                ymaxElement.InnerText = (obj.Y + obj.H).ToString();
                bndboxElement.AppendChild(ymaxElement);

            }
            // tao dau tab dau dong cho tat ca cac element xml
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                IndentChars = "\t",
                Encoding = System.Text.Encoding.UTF8
            };


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
    internal class XmlExporter
    {
        public IDictionary<int, string> _dict;
        public YoloModelBase _model;
        public XmlExporter() { }
        class ObjectDoor
        {
            private int xmin;
            private int ymin;
            private int xmax;
            private int ymax;
            private string name;
            public ObjectDoor()
            {
                this.Ymin = 0;
                this.Xmax = 0;
                this.Ymax = 0;
                this.Xmin = 0;
                this.Name = "";
            }
            public ObjectDoor(int xmin, int ymin, int xmax, int ymax, string name)
            {
                this.Ymin = ymin;
                this.Xmax = xmax;
                this.Ymax = ymax;
                this.Xmin = xmin;
                this.Name = name;
            }
            public int Xmin { get => xmin; set => xmin = value; }
            public int Ymin { get => ymin; set => ymin = value; }
            public int Xmax { get => xmax; set => xmax = value; }
            public int Ymax { get => ymax; set => ymax = value; }
            public string Name { get => name; set => name = value; }
        }

        public void Run(string capture_input, string sample_output)
        {
            var cc = new List<(string, string)> {
                ($@"{capture_input}\cc_1.jpeg",$@"{sample_output}\front_01.xml"),
                ($@"{capture_input}\cc_2.jpeg",$@"{sample_output}\front_02.xml"),
                ($@"{capture_input}\cc_3.jpeg",$@"{sample_output}\back_01.xml"),
                ($@"{capture_input}\cc_4.jpeg",$@"{sample_output}\back_02.xml"),
            };

            foreach (var (img, xml) in cc)
            {
                // detect all image inputs
                var pbc = _model.Detect(img, null);

                var xmlPath = xml;
                var xmlTxt = Parser.ExportXml(Path.GetFileName(Path.GetDirectoryName(img)),
                    filename: Path.GetFileName(img),
                    path: img,
                    pbc.boxes.ConvertAll(x => new Box
                    {
                        rectangle = x.rectangle,
                        LabelName = x.LabelName,
                    }));
                File.WriteAllText(xmlPath, xmlTxt);
            }
        }

        internal static void CreateXmls(QuarterTrim qt, string sample_output)
        {
            var cc = new List<(Panel, string)> {
                (qt.Panels.FirstOrDefault(p=>p.Type == PanelType.FIRST_FRONT),$@"{sample_output}\front_01.xml"),
                (qt.Panels.FirstOrDefault(p=>p.Type == PanelType.SECOND_FRONT),$@"{sample_output}\front_02.xml"),
                (qt.Panels.FirstOrDefault(p=>p.Type == PanelType.FIRST_BACK),$@"{sample_output}\back_01.xml"),
                (qt.Panels.FirstOrDefault(p=>p.Type == PanelType.SECOND_BACK),$@"{sample_output}\back_02.xml"),
            };

            foreach (var (panel, xml) in cc)
            {
                var xmlPath = xml;
                var xmlTxt = Parser.ExportXml("unknown",
                    filename: "unknown",
                    path: "unknown",
                    panel.Parts.ToList().ConvertAll(x => new Box
                    {
                        X = (float)x.PosX,
                        Y = (float)x.PosY,
                        W = (float)x.PosW,
                        H = (float)x.PosH,
                        LabelName = x.Name,
                    }));
                File.WriteAllText(xmlPath, xmlTxt);
            }
        }

        internal static QuarterTrim CreateConfig(string sample_output)
        {
            QuarterTrim qt = new QuarterTrim();
            var cc = new List<(PanelType, string)> {
                (PanelType.FIRST_FRONT,$@"{sample_output}\front_01.xml"),
                (PanelType.SECOND_FRONT,$@"{sample_output}\front_02.xml"),
                (PanelType.FIRST_BACK,$@"{sample_output}\back_01.xml"),
                (PanelType.SECOND_BACK,$@"{sample_output}\back_02.xml"),
            };

            qt.Panels = cc.ConvertAll((x) =>
            {
                var (panelType, xml) = x;
                var xmlPath = xml;
                var (_ImgSize, raw) = FRHelper.ReadXml(xmlPath);
                var panel = new Panel
                {
                    Type = panelType,
                    Parts = raw.ConvertAll(obj => new Part
                    {
                        PosX = (int)obj.X,
                        PosY = (int)obj.Y,
                        PosH = (int)obj.H,
                        PosW = (int)obj.W,
                        Name = obj.LabelName,
                    }),
                };
                return panel;
            });

            return qt;
        }
    }
}
