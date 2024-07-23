using Newtonsoft.Json;
using Repositories;

namespace Models
{
    //[JsonConverter(typeof(StringEnumConverter))]
    public enum PanelType
    {
        FIRST_FRONT = 0,
        FIRST_BACK = 1,
        SECOND_FRONT = 2,
        SECOND_BACK = 3,
    }
    public class Panel : IEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; } = RepositoryHelper.NewId();
        [JsonProperty("type")]
        public PanelType? Type { get; set; }
        [JsonProperty("beforeImgId")]
        public string BeforeImgId { get; set; }
        //public virtual Files BeforeImg { get; set; }
        [JsonProperty("resultImgId")]

        public string ResultImgId { get; set; }
        public virtual FileEntity ResultImg { get; set; }
        public virtual FileEntity BeforeImg { get; set; }
        [JsonProperty("quarterTrimId")]
        public string QuarterTrimId { get; set; }
        //public virtual QuarterTrim QuarterTrim { get; set; }

        public virtual IEnumerable<Part> Parts { get; set; }
    }


}
