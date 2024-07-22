using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;

namespace Extensions
{
    public static class JsonExtensions
    {
        static readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new BoxConverter(), new StringEnumConverter(), new MyBoxConverter() },
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
        };
        public static string ToJson(this Object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, jsonSerializerSettings);
        }
        public static T FromJson<T>(this string json)
        {
            return JsonConvert.DeserializeObject<T>(json, jsonSerializerSettings);
        }
    }
}
