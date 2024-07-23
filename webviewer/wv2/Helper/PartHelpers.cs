using Models;
using WP_GUI.ModelAlgorithm;

public static class PartHelpers
{
    public static List<Part> CreatePartEntitys(List<ImgObject> outObj, List<bool> res)
    {
        List<Part> listParts = new List<Part>();
        //foreach(var (rectCustom, boolValue, d) in listJdg)
        //{
        //    Part part = new Part();
        //    part.Name = rectCustom.name;part.PosX = rectCustom.x;part.PosY = rectCustom.y;
        //    part.PosW = rectCustom.w;part.PosH = rectCustom.h;part.Percent = rectCustom.percent;
        //    part.PanelId = panel.Id;part.Judge = boolValue;
        //    listParts.Add(part);
        //}
        //int length = outObj.Count;
        for (int j = 0; j < outObj.Count; j++)
        {
            Part part = new Part
            {
                Judge = res[j],
                Name = outObj[j].LabelName,
                PosX = (int)outObj[j].X,
                PosY = (int)outObj[j].Y,
                PosW = (int)outObj[j].W,
                PosH = (int)outObj[j].H,
                Percent = (int)(outObj[j].score * 100),
                //PanelId = panel.Id,
            };
            listParts.Add(part);
        }

        // sort by name and x,y for the same position of object in panels
        listParts.Sort((a, b) =>
        {
            var i1 = a.Name.CompareTo(b.Name);
            if (i1 != 0)
                return i1;
            var i2 = (int)a.PosX - (int)b.PosX;
            if (i2 != 0)
                return i2;
            var i3 = (int)a.PosY - (int)b.PosY;
            return i3;
        });

        return listParts;
    }
}