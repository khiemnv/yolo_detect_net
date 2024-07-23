using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Drawing.Imaging;
//using System.Text.Json;

#if true
public static class ObjExtensions
{
    static readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Converters = { new BoxConverter(), new StringEnumConverter() },
        NullValueHandling = NullValueHandling.Ignore,
        //Formatting = Formatting.Indented,
    };
    public static string ToJson(this Object obj)
    {
        return JsonConvert.SerializeObject(obj, jsonSerializerSettings);
    }
    public static T FromJson<T>(this string json)
    {
        return JsonConvert.DeserializeObject<T>(json, jsonSerializerSettings);
    }

    public static bool SaveAsJpeg(this Bitmap img, string path)
    {
        ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
        EncoderParameters myEncoderParameters = new EncoderParameters(1);
        System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
        EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 90L);
        myEncoderParameters.Param[0] = myEncoderParameter;
        img.Save(path, jpgEncoder, myEncoderParameters);
        return true;
    }
    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
        foreach (ImageCodecInfo codec in codecs)
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }
        return null;
    }

    public static Bitmap Base64StringToBitmap(this string base64String)
    {
        byte[] byteBuffer = Convert.FromBase64String(base64String);
        MemoryStream memoryStream = new MemoryStream(byteBuffer)
        {
            Position = 0
        };

        //Bitmap bmpReturn = (Bitmap)Image.FromStream(memoryStream);
        Bitmap bmpReturn = new Bitmap(memoryStream);
        //memoryStream.Close();
        return bmpReturn;
    }
}
#else
        static JsonSerializerOptions options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        public static string ToJson(this Object obj)
        {
            return JsonSerializer.Serialize(obj, options);
        }
        public static T FromJson<T>(this string json)
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
#endif


public sealed class SizedQueue<T> : Queue<T>
{
    public int FixedCapacity { get; }
    public SizedQueue(int fixedCapacity)
    {
        this.FixedCapacity = fixedCapacity;
    }

    /// <summary>
    /// If the total number of item exceed the capacity, the oldest ones automatically dequeues.
    /// </summary>
    /// <returns>The dequeued value, if any.</returns>
    public new T Enqueue(T item)
    {
        base.Enqueue(item);
        if (base.Count > FixedCapacity)
        {
            return base.Dequeue();
        }
        return default;
    }
}
