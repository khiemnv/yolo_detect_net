// See https://aka.ms/new-console-template for more information
using annotation;
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
        i3.Replace(".jpeg", "_crop.jpeg"));
    Debug.WriteLine($"sample angle {angle}");

    var segmented = $"{x} {y} {w} {h} {angle} {area}";

    var back_01 = @"C:\deploy\doordetect\workingDir\model_7230B289XA\sample_7230B289XA\output\back_01.xml";
    var xmlTxt = Parser.ExportXml(folder: "", filename: Path.GetFileName(i3), path: "", pcb.boxes, segmented);
    File.WriteAllText(back_01, xmlTxt);
}