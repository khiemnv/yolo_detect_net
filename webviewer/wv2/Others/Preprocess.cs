using FR;
using Models;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using WP_GUI.ModelAlgorithm;
using Yolov;
using static PanelHelpers;
using static ProgramHelpers;
using Panel = Models.Panel;

public interface IPreprocess
{
    void Crop(string orgPath, string cropPath);
    (List<bool> res, List<ImgObject> outObj) FilterAndRestruct(float delta, string pathXml, PredictionBoxCollection pcb, string imgPath);
    void InitSession(QuarterTrim qt);
}

public class PreprocessBase
{
    protected class SessionQT
    {
        public QuarterTrim qt;
        public class QTImg
        {
            public QTImg(Panel panel) { _panel = panel; }
            public string Org => _panel.BeforeImg?.Path;
            public string Res => _panel.ResultImg?.Path;
            internal double target_angle;
            internal SegmentationBoundingBox seg;
            internal int target_x;
            internal int target_y;
            internal int target_w;
            internal int target_h;
            internal string crop;
            private readonly Panel _panel;
        }
        public List<QTImg> imgs;
    }

    protected SessionQT _session;
    public void InitSession(QuarterTrim qt)
    {
        _session = new SessionQT();
        _session.qt = qt;
        _session.imgs = qt.Panels.ToList().ConvertAll(panel => new SessionQT.QTImg(panel));
    }
}

public class PreprocessManual : PreprocessBase, IPreprocess
{
    // crop
    public Rectangle cropLeft;
    public Rectangle cropRight;

    public PreprocessManual(IEnumerable<int> cropLeft, IEnumerable<int> cropRight)
    {
        var arrL = cropLeft;
        this.cropLeft = new Rectangle(arrL.ElementAt(0), arrL.ElementAt(1), arrL.ElementAt(2), arrL.ElementAt(3));
        var arrR = cropRight;
        this.cropRight = new Rectangle(arrR.ElementAt(0), arrR.ElementAt(1), arrR.ElementAt(2), arrR.ElementAt(3));
    }

    public void Crop(string orgPath, string cropPath)
    {
        var idx = (ImgListIndex)(_session.imgs.FindIndex(x => x.Org == orgPath));
        var rec = idx.HasFlag(ImgListIndex.fRight) ? cropRight : cropLeft;

        // create crop
        var org = new Bitmap(orgPath);
        var crop = Crop1(org, rec);

        // save crop
        crop.SaveAsJpeg(cropPath);

        // release img
        crop.Dispose();
        org.Dispose();
    }
    Bitmap Crop1(Bitmap org, Rectangle _rec)
    {
        Bitmap crop = new Bitmap(org.Width, org.Height);
        using (Graphics gfx = Graphics.FromImage(crop))
        {
            using (SolidBrush brush = new SolidBrush(Color.Gray))
            {
                gfx.FillRectangle(brush, 0, 0, org.Width, org.Height);
            }
            gfx.DrawImage(org, _rec, _rec, GraphicsUnit.Pixel);
        };
        return crop;
    }

    public virtual (List<bool> res, List<ImgObject> outObj) FilterAndRestruct(float delta, string pathXml, PredictionBoxCollection pbc, string imgPath)
    {
        List<ImgObject> detectedBoxes = pbc.boxes.ConvertAll(box => new ImgObject(box));
        var reg = new Regex("^(ng|ig)_"); // NG or IGNORE

        // rename boxes
        detectedBoxes.ForEach(box => { if (_dict2.ContainsKey(box.LabelName)) { box.LabelName = _dict2[box.LabelName]; } });

        // filter OK boxes
        var okBoxes = detectedBoxes.Where(obj => !reg.IsMatch(obj.LabelName)).ToList();

        // init output
        List<ImgObject> outObj = new List<ImgObject> { new ImgObject("default", 0, 0, 1, 1, 0) };
        List<bool> res = new List<bool> { false };
        var (_ImgSize, raw) = FRHelper.ReadXml(pathXml);

        // rename boxes
        raw.ForEach(img => { if (_dict2.ContainsKey(img.LabelName)) { img.LabelName = _dict2[img.LabelName]; } });

        // filter boxes
        var _LReferenceObj = raw.Where(obj => !reg.IsMatch(obj.LabelName)).ToList();

        try
        {
            ModelAlgorithm model = new ModelAlgorithm(_ImgSize, _LReferenceObj);
            if (okBoxes.Count < 4)
            {
                throw new Exception("Too few boxes");
            }
            else
            {
                // re-construct
                res = model.Reconstruction(okBoxes, out outObj, delta);
                if (res == null || outObj == null || res.Count != outObj.Count)
                {
                    throw new Exception("Reconstruction fail!");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            okBoxes.Sort();
            _LReferenceObj.Sort();
            var diff = _LReferenceObj.ConvertAll(refBox =>
            {
                var found = okBoxes.Find(box => box.LabelName == refBox.LabelName);
                if (found != null)
                {
                    okBoxes.Remove(found);
                    return (found, true);
                }
                else
                {
                    return (refBox, false);
                }
            });
            outObj = diff.Select(cell => cell.Item1).ToList();
            res = diff.Select(cell => cell.Item2).ToList();
        }
        finally
        {
            // validate
            if (res.Count != outObj.Count)
            {
                Logger.Debug(new { okBoxes, outObj, res }.ToJson());
                if (res.Count < outObj.Count)
                {
                    res.AddRange(Enumerable.Repeat(false, outObj.Count - res.Count).ToList());
                }
                else
                {
                    res = res.GetRange(0, outObj.Count);
                }
                Logger.Error($"Reconstruction fail! res.Count:{res.Count} outObj.Count:{outObj.Count}");
            }

            //if (res.Any(r => r == false))
            //{
            //    Logger.Debug(new { okBoxes, outObj, res, score }.ToJson());
            //}
        }

        return (res, outObj);
    }


}

public class PreprocessAuto : PreprocessBase, IPreprocess, IDisposable
{
    protected string segModelPath;
    protected YoloModelBase Yolo;
    public PreprocessAuto(string modelFile)
    {
        if (!File.Exists(modelFile))
        {
            throw new Exception($"crop model file not exist! [{modelFile}]");
        }
        segModelPath = modelFile;
        LoadSegModel(segModelPath);
    }
    void LoadSegModel(string segModelPath)
    {
        // load model
        if (Yolo == null)
        {
            Yolo = new VinoYolo8
            {
                Overlap = 0.45f,
                ModelConfidence = 0.6f,
            };
            Yolo.SegDetect(null, segModelPath);
        }
    }
    public void Crop(string orgPath, string cropPath)
    {
        SegmentAndCrop(orgPath, cropPath);
    }

    void SegmentAndCrop(string imgPath, string crop)
    {
        // seg detect (target)
        var segs = Yolo.SegDetect(imgPath, null);
        var idx = 0;
        for (var i = 1; i < segs.Count; i++)
        {
            if (segs[i].confidence > segs[idx].confidence) { idx = i; }
        }
        var seg = segs[idx];

        var (target_x, target_y, target_w, target_h) = (seg.bounds.X, seg.bounds.Y, seg.bounds.Width, seg.bounds.Height);

        var (target_angle, target_area) = PlotCV.Crop(imgPath,
            seg.mask,
            (seg.bounds.X, seg.bounds.Y, seg.bounds.Width, seg.bounds.Height),
            crop);

        // update img qt
        var imgQtIdx = _session.imgs.FindIndex(x => x.Org == imgPath);
        var imgQt = _session.imgs[imgQtIdx];
        imgQt.seg = seg;
        imgQt.target_angle = target_angle;
        imgQt.target_x = target_x;
        imgQt.target_y = target_y;
        imgQt.target_w = target_w;
        imgQt.target_h = target_h;
        imgQt.crop = crop;
    }
    public (List<bool> res, List<ImgObject> outObj) FilterAndRestruct(float delta, string pathXml, PredictionBoxCollection pcb, string imgPath)
    {
        // load seg detect model
        //LoadSegModel();

        //SegmentAndCrop()
        var imgQtIdx = _session.imgs.FindIndex(x => x.crop == imgPath);
        var imgQt = _session.imgs[imgQtIdx];
        var seg = imgQt.seg;
        var target_angle = imgQt.target_angle;
        var (target_x, target_y, target_w, target_h) = (seg.bounds.X, seg.bounds.Y, seg.bounds.Width, seg.bounds.Height);
        var crop = imgQt.crop;
        //var (target_angle, target_area, _) = PlotCV.GetContour(seg.mask);

        // load config
        var cfg = FRNotation.ParseXml(pathXml);
        var arr = cfg.segmented.Split(' ');
        var sample_x = int.Parse(arr[0]);
        var sample_y = int.Parse(arr[1]);
        var sample_w = int.Parse(arr[2]);
        var sample_h = int.Parse(arr[3]);
        var sample_angle = double.Parse(arr[4]);
        var sample_area = double.Parse(arr[5]);
        var sample_cx = double.Parse(arr[6]);
        var sample_cy = double.Parse(arr[7]);

        if (Math.Abs(target_angle - sample_angle) > 15)
        {
            throw new Exception("Can not re-struct");
        }

        // rotate (sample)
        var sampleBoxes = new List<PredictionBox>();
        Pen myPen = new Pen(Color.FromArgb(127, Color.Blue), 10);
        Pen myPen2 = new Pen(Color.FromArgb(127, Color.Yellow), 10);
        Pen myPen3 = new Pen(Color.FromArgb(127, Color.Red), 10);

        // Create an array of points.
        List<System.Drawing.Point> myLst = new List<Point>
                         {
                     new Point(sample_x, sample_y ),
                     new Point(sample_x+sample_w, sample_y +sample_h),
                 };
        foreach (var box in cfg.boxes)
        {
            myLst.Add(new Point(box.bndbox.xmin, box.bndbox.ymin));
            myLst.Add(new Point(box.bndbox.xmax, box.bndbox.ymax));
        }

        // Draw the Points to the screen before applying the
        // transform.
        //var bitmap = ImageExtensions.CloneFromFile(crop);
        var myArray = myLst.ToArray();
        var dx = target_x - myArray[0].X;
        var dy = target_y - myArray[0].Y;
        //using (Graphics g = Graphics.FromImage(bitmap))
        {
            // translate
            //for (int i = 0; i < myArray.Count(); i += 2)
            //{
            //    var (xmin, ymin) = (myArray[i].X, myArray[i].Y);
            //    var (xmax, ymax) = (myArray[i + 1].X, myArray[i + 1].Y);
            //    g.DrawRectangle(myPen, xmin + dx, ymin + dy, xmax - xmin, ymax - ymin);
            //}

            // Create a matrix and rotate.
            System.Drawing.Drawing2D.Matrix myMatrix = new System.Drawing.Drawing2D.Matrix();
            var c = new PointF(cfg.size.width / 2, cfg.size.height / 2);
            myMatrix.RotateAt((float)(target_angle - sample_angle), c, MatrixOrder.Append);
            myMatrix.TransformPoints(myArray);

            //dx = target_x - myArray[0].X;
            //dy = target_y - myArray[0].Y;
            //for (int i = 0; i < myArray.Count(); i += 2)
            //{
            //    var (xmin, ymin) = (myArray[i].X, myArray[i].Y);
            //    var (xmax, ymax) = (myArray[i + 1].X, myArray[i + 1].Y);
            //    g.DrawRectangle(myPen2, xmin, ymin, xmax - xmin, ymax - ymin);
            //}

            // Create a matrix and scale it.
            var minX = myArray[0].X;
            var minY = myArray[0].Y;
            var maxX = myArray[1].X;
            var maxY = myArray[1].Y;
            var scaleX = target_w / (float)(maxX - minX);
            var scaleY = target_h / (float)(maxY - minY);
            myMatrix = new Matrix();
            myMatrix.Scale(scaleX, scaleY, MatrixOrder.Append);
            myMatrix.TransformPoints(myArray);

            //Draw the Points to the screen again after applying the transform.

            // translate
            dx = target_x - myArray[0].X;
            dy = target_y - myArray[0].Y;
            for (int i = 2; i < myArray.Count(); i += 2)
            {
                var (xmin, ymin) = (myArray[i].X, myArray[i].Y);
                var (xmax, ymax) = (myArray[i + 1].X, myArray[i + 1].Y);
                var rect = new System.Drawing.Rectangle(xmin + dx, ymin + dy, xmax - xmin, ymax - ymin);
                //g.DrawRectangle(myPen3, rect);

                sampleBoxes.Add(new PredictionBox { X = rect.X, Y = rect.Y, W = rect.Width, H = rect.Height });
            }
        }

        for (int i = 0; i < cfg.boxes.Count; i++)
        {
            sampleBoxes[i].LabelName = cfg.boxes[i].label;
        }

#if false
        //debug

        var bitmap = ImageExtensions.CloneFromFile(crop);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            sampleBoxes.ForEach(box =>
            {
                g.DrawRectangle(myPen3, box.X, box.Y, box.W, box.H);
            });
        }
        bitmap.Save("re-struct_" + Path.GetFileName(imgPath));
        // compare

        myLst = new List<System.Drawing.Point>
                {
                     new System.Drawing.Point(target_x, target_y),
                     new System.Drawing.Point(target_x  +target_w, target_y +target_h),
                 };
        foreach (var box in pcb.boxes)
        {
            myLst.Add(new Point((int)box.X, (int)box.Y));
            myLst.Add(new Point((int)(box.X + box.W), (int)(box.Y + box.H)));
        }
        myArray = myLst.ToArray();
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            for (int i = 2; i < myArray.Count(); i += 2)
            {
                var (xmin, ymin) = (myArray[i].X, myArray[i].Y);
                var (xmax, ymax) = (myArray[i + 1].X, myArray[i + 1].Y);
                g.DrawRectangle(new Pen(Color.FromArgb(127, Color.Green), 10), xmin, ymin, xmax - xmin, ymax - ymin);
            }
        }

        bitmap.Save("crop_" + Path.GetFileName(imgPath));
        bitmap.Dispose();
#endif

        var sampleBoxes2 = RenameAndFilter(sampleBoxes);
        var detectedBoxes = RenameAndFilter(pcb.boxes);

        List<bool> res = new List<bool>();
        List<ImgObject> outObj = new List<ImgObject>();
        sampleBoxes2.ForEach(sampleBox =>
        {
            var c2 = new PointF(sampleBox.X + sampleBox.W / 2, sampleBox.Y + sampleBox.H / 2);
            var found = detectedBoxes.Find(detectBox =>
            {
                if (detectBox.LabelName != sampleBox.LabelName) return false;

                var c1 = new PointF(detectBox.X + detectBox.W / 2, detectBox.Y + detectBox.H / 2);
                var d = Distance(c1, c2);
                if (d > delta) return false;

                return true;
            });

            if (found != null)
            {
                res.Add(true);
                outObj.Add(new ImgObject(found));
                detectedBoxes.Remove(found);
            }
            else
            {
                outObj.Add(new ImgObject(sampleBox));
                res.Add(false);
            }
        });

        return (res, outObj);
    }

    public void Dispose()
    {
        if (Yolo != null)
        {
            Yolo.Dispose();
            Yolo = null;
        }
    }
}