using System.Xml;
using WP_GUI.ModelAlgorithm;

public static class FRHelper
{
    public static (ImageSize, List<ImgObject>) ReadXml(string XmlConfigPath)
    {
        ImageSize _ImgSize = new ImageSize();
        List<ImgObject> _LReferenceObj;
        XmlDocument xmlFile = new XmlDocument();
        xmlFile.Load(XmlConfigPath);

        var ImgSize = xmlFile.DocumentElement.SelectSingleNode("size");
        _ImgSize.width = Int32.Parse(ImgSize.ChildNodes[0].InnerText);
        _ImgSize.height = Int32.Parse(ImgSize.ChildNodes[1].InnerText);
        _ImgSize.depth = Int32.Parse(ImgSize.ChildNodes[2].InnerText);


        var ObjNodes = xmlFile.DocumentElement.SelectNodes("object") ?? throw new Exception("invalid xml");
        _LReferenceObj = ModelAlgorithm.getImgObject(ObjNodes);
        return (_ImgSize, _LReferenceObj);
    }
}