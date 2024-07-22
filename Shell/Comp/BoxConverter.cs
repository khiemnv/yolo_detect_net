using comp;
using Newtonsoft.Json;
using System;
using Yolov;

namespace Extensions
{
    public class BoxConverter : JsonConverter<Box>
    {
        public override void WriteJson(JsonWriter writer, Box value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.LabelName} {value.X:0.##} {value.Y:0.##} {value.W:0.##} {value.H:0.##} {value.score:0.##}");
        }

        public override Box ReadJson(JsonReader reader, Type objectType, Box existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var fullName = serializer.Deserialize<string>(reader);
            var arr = fullName.Split(' ');
            var box = new Box
            {
                LabelName = arr[0],
                rectangle = PredictionBox.RectangleF(float.Parse(arr[1]), float.Parse(arr[2]), float.Parse(arr[3]), float.Parse(arr[4])),
                score = float.Parse(arr[5]),
            };
            return box;
        }
    }
}
