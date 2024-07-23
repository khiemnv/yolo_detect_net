using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Yolov;

namespace FR
{
    public class FasterRetinanetVino : YoloModelBase
    {
        CompiledModel compiled_model;
        InferRequest infer_request;
        float[] input_data;
        OpenVinoSharp.Tensor input_tensor;
        Shape input_shape;
        OpenVinoSharp.Tensor output_tensor;
        Shape output_shape;
        int output_length;
        private Core core;
        private Model model;
        private string ModelInputName;

        public static List<string> ParsePbtxt(string path)
        {
            // parse
            var dict = ReadLabelDict(path);
            return dict.Values.ToList();
        }

        protected override void ReadModel(string model_path)
        {
            core = new Core();
            model = core.read_model(model_path);
            if (DeviceDetect == "multi")
            {
                var dict = new Dictionary<string, string>() {
                    { "MULTI_DEVICE_PRIORITIES", "GPU,CPU" }
                };
                compiled_model = core.compile_model(model, "MULTI", dict);
            }
            else
            {
                compiled_model = core.compile_model(model, "AUTO");
            }

            infer_request = compiled_model.create_infer_request();
            input_tensor = infer_request.get_tensor("input_tensor");
            input_shape = input_tensor.get_shape();
            // 4656*3496
            ModelInputHeight = 640;
            ModelInputWidth = 640;
            input_data = new float[ModelInputHeight * ModelInputWidth * 3];

            //output_tensor = infer_request.get_tensor("output0");
            //output_shape = output_tensor.get_shape();
            //output_length = (int)output_tensor.get_size();

            // metadata.yaml or dataset.yaml or names.json
            var parent = System.IO.Path.GetDirectoryName(model_path);
            var labelMap = Path.GetDirectoryName(model_path) + "\\label_map.pbtxt";
            if (File.Exists(labelMap))
            {
                labelDict = ReadLabelDict(labelMap);
            }
            else
            {
                throw new Exception("missing label info");
            }

            labels = labelDict.ToList().ConvertAll(p => new PredictionBox.Label { id = p.Key, name = p.Value });

            ModelClassesCount = labelDict.Count();

        }

        public override PredictionBoxCollection Detect(string image_path)
        {
            Mat image = new Mat(image_path); // Read image by opencvsharp
            int max_image_length = image.Cols > image.Rows ? image.Cols : image.Rows;
            Mat max_image = Mat.Zeros(new OpenCvSharp.Size(max_image_length, max_image_length), MatType.CV_8UC3);
            Rect roi = new Rect(0, 0, image.Cols, image.Rows);
            image.CopyTo(new Mat(max_image, roi));

            float factor = (float)max_image_length / (float)Math.Max(ModelInputHeight, ModelInputWidth);
            Mat input_mat = CvDnn.BlobFromImage(max_image, 1.0 / 255.0, new OpenCvSharp.Size(ModelInputHeight, ModelInputWidth), 0, true, false);
            Marshal.Copy(input_mat.Ptr(0), input_data, 0, input_data.Length);

            input_tensor.set_data<float>(input_data);
            infer_request.infer();
            List<float[]> outputs = new List<float[]>();
            var output_tensor = infer_request.get_tensor("detection_boxes");
            var output_shape = output_tensor.get_shape();
            var output_length = (int)output_tensor.get_size();
            float[] output_data = output_tensor.get_data<float>(output_length);

            var boxes = ParseOutput(outputs, image.Width, image.Height, factor);
            var pbc = new PredictionBoxCollection { w = image.Width, h = image.Height, boxes = boxes };
            return pbc;
        }


        protected List<PredictionBox> ParseOutput(List<float[]> outputs, int w, int h, float factor)
        {
            var len = 50;
            var t_boxes = new Mat(50, 4, MatType.CV_32F, outputs[0]); // [1,50,4]  xmin ymin xmax ymax
            var t_labelIdxs = new Mat(1, 50, MatType.CV_32F, outputs[1]); // [1,50]
            var t_scores = new Mat(1, 50, MatType.CV_32F, outputs[2]); // [1,50]

            var result = new ConcurrentBag<PredictionBox>();
            Parallel.For(0, len, (i) =>
            {
                var confidence = t_scores.At<float>(0, i);
                if (confidence <= ModelConfidence) return; // skip low obj_conf results

                var xMin = t_boxes.At<float>(i, 1) * w;
                var yMin = t_boxes.At<float>(i, 0) * h;
                var xMax = t_boxes.At<float>(i, 3) * w;
                var yMax = t_boxes.At<float>(i, 2) * h;

                xMin = Clamp(xMin, 0, w - 0); // clip bbox tlx to boundaries
                yMin = Clamp(yMin, 0, h - 0); // clip bbox tly to boundaries
                xMax = Clamp(xMax, 0, w - 1); // clip bbox brx to boundaries
                yMax = Clamp(yMax, 0, h - 1); // clip bbox bry to boundaries

                var k = (int)t_labelIdxs.At<float>(0, i);
                PredictionBox.Label label = labels[k - 1]; // one base

                var prediction = new PredictionBox()
                {
                    LabelName = label.name,
                    score = confidence,
                    rectangle = new PredictionBox.MyRect(xMin, yMin, xMax - xMin, yMax - yMin),
                    index = i,
                };

                result.Add(prediction);
            });

            return result.ToList();
        }

        public static Dictionary<int, string> ReadLabelDict(string labelMap)
        {
            var dict = new Dictionary<int, string>();
            var txt = File.ReadAllText(labelMap);
            var m = System.Text.RegularExpressions.Regex.Matches(txt, @"item {[\s\r\n]+id: (\d+)[\s\r\n]+name: '(\w+)'[\s\r\n]+}", RegexOptions.Multiline);
            foreach (System.Text.RegularExpressions.Match i in m)
            {
                Debug.Assert(dict.Count == (int.Parse(i.Groups[1].Value) - 1));
                dict.Add(dict.Count, i.Groups[2].Value);
            }
            return dict;
        }
    }
}
