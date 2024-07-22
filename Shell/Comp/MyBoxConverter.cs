using annotation;
using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;

namespace Extensions
{
    public class MyBoxConverter : JsonConverter<MyBox>
    {
        public override void WriteJson(JsonWriter writer, MyBox value, JsonSerializer serializer)
        {
            var rect = value.Rect;
            writer.WriteValue($"{value.LabelName} {rect.X} {rect.Y} {rect.Width} {rect.Height} {value.bmp.Width} {value.bmp.Height}");
        }

        public override MyBox ReadJson(JsonReader reader, Type objectType, MyBox existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            try
            {
                var raw = serializer.Deserialize<string>(reader);
                if (!Regex.IsMatch(raw, @"\d+ \d+ \d+ \d+ \d+ \d+ ")) { return null; }
                var arr = raw.Split(' ');
                var rect = new System.Drawing.Rectangle(
                    int.Parse(arr[1]),
                    int.Parse(arr[2]),
                    int.Parse(arr[3]),
                    int.Parse(arr[4])
                    );
                var newObj = new MyBox
                {
                    LabelName = arr[0],
                    bmp = (int.Parse(arr[5]), int.Parse(arr[6])),
                    Rect = rect,
                };
                return newObj;
            }
            catch (Exception ex)
            {
                Logger.Logger.Error(ex.Message);
                return null;
            }
        }
    }
}
