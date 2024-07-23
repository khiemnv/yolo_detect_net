using Models;

public static class PanelHelpers
{
    //[0]first front
    //[1]second front
    [Flags]
    public enum ImgListIndex
    {
        fSecond = 1,
        fRight = 2,
        firstLeft = 0,
        firstRight = 2,
        secondLeft = 1,
        secondRight = 3,
    }
    //public static Panel CreatePanelEntity(FileEntity fileResult, int idx, FileEntity fileInput)
    //{

    //    //listRt = listJdg.ConvertAll(t => t.Item2);

    //    //send file imgresult, file ng_ok, 1 panel
    //    Panel panel = new Panel
    //    {
    //        BeforeImgId = fileInput.Id,
    //        ResultImgId = fileResult.Id
    //    };
    //    //panel.ResultImg = fileResult;
    //    //panel.QuarterTrimId = qt1.Id;
    //    UpdatePanelType(panel, idx);
    //    return panel;
    //}

    public static PanelType ImgListIndexToPanelType(ImgListIndex idx)
    {
        switch (idx)
        {
            case ImgListIndex.firstLeft:
                return PanelType.FIRST_FRONT;
            case ImgListIndex.firstRight:
                return PanelType.SECOND_FRONT;
            case ImgListIndex.secondLeft:
                return PanelType.FIRST_BACK;
            case ImgListIndex.secondRight:
            default:
                return PanelType.SECOND_BACK;
        }
    }
}