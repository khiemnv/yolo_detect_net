// See https://aka.ms/new-console-template for more information
using annotation;
using OpenCvSharp;
using System.Diagnostics;
using Yolov;

Console.WriteLine("Hello, World!");
if (true)
{
    // create config (sample)
    var i3 = @"C:\deploy\doordetect\workingDir\model_7230B289XA\sample_7230B289XA\test\10cc810ba79c2000496faad6b9bc9c81_0.jpeg";
    var ObjDetectYolo = new VinoYolo8
    {
        Overlap = 0.45f,
        ModelConfidence = 0.6f,
    };
    ObjDetectYolo.Detect(null, @"C:\deploy\doordetect\workingDir\model_7230B289XA\sample_7230B289XA\output\openvino\best.xml");
    var pcb = ObjDetectYolo.Detect(i3, null);

    var SegDetectYolo = new VinoYolo8
    {
        Overlap = 0.45f,
        ModelConfidence = 0.6f,
    };
    SegDetectYolo.SegDetect(null, @"C:\yolo\model_1234AB1SEG\models\openvino\best.xml");
    var segs = SegDetectYolo.SegDetect(i3, null);
    var (x, y, w, h) = (segs[0].bounds.X, segs[0].bounds.Y, segs[0].bounds.Width, segs[0].bounds.Height);
    var (angle, area) = PlotCV.Crop(i3,
        segs[0].mask,
        (segs[0].bounds.X, segs[0].bounds.Y, segs[0].bounds.Width, segs[0].bounds.Height),
        "crop.jpeg");
    Debug.WriteLine($"sample angle {angle}");


    var originImage = new OpenCvSharp.Mat("crop.jpeg");
    var cp = new ColorPalette();
    cp.Init(new LabelConfig { _dict = ObjDetectYolo.labelDict });
    foreach(var box in pcb.boxes)
    {
        var fontScale = 3;
        var fontFace = HersheyFonts.HersheyDuplex;
        var thickness = 3;
        var alpha = 0.5;
        var padding = 10;
        Cv2.Rectangle(originImage, new Rect((int)box.X, (int)box.Y, (int)box.W, (int)box.H), Scalar.Blue, thickness);
        var labelSize = Cv2.GetTextSize(box.LabelName, fontFace, fontScale, 10, out int _);
        var minX = (int)box.X;
        var minY = (int)box.Y - labelSize.Height - 2*padding;
        var maxX = minX + labelSize.Width;
        if (minX > originImage.Width) { maxX = originImage.Width; }
        if (minY < 0) { minY = 0; }
        var maxY = (int)box.Y;
        var labelRect = new Rect(minX, minY, maxX-minX, maxY-minY);
        var color = cp.GetColor(box.LabelName).color;
        var subImg = originImage.SubMat(labelRect);
        var blueRect = subImg.Clone();
        Cv2.Rectangle(blueRect, new Rect(0,0, maxX - minX, maxY - minY), Scalar.FromRgb(color.r,color.g, color.b), -1);
        Cv2.AddWeighted(subImg, alpha, blueRect, 1- alpha, 1.0, subImg);
        Cv2.PutText(subImg, box.LabelName, new Point(0, labelSize.Height + padding), fontFace, fontScale, Scalar.White, thickness);
        //subImg.SaveImage("subImg.jpeg");
        //Cv2.PutText(originImage, box.LabelName, new Point(box.X, box.Y), fontFace, fontScale, Scalar.White, thickness);
        originImage[labelRect] = subImg;
        //originImage.SaveImage("crop2.jpeg");
    }
    originImage.SaveImage("crop2.jpeg");

    var segmented = $"{x} {y} {w} {h} {angle} {area}";

    //var back_01 = @"C:\deploy\doordetect\workingDir\model_7230B289XA\sample_7230B289XA\output\back_01.xml";
    //var xmlTxt = Parser.ExportXml(folder: "", filename: Path.GetFileName(i3), path: "", pcb.boxes, segmented);
    //File.WriteAllText(back_01, xmlTxt);
}