using Newtonsoft.Json;
using Repositories;

namespace Models
{
    public class DetectModel : IEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; } = RepositoryHelper.NewId();
        [JsonProperty("partNumber")]
        public string PartNumber { get; set; }
        [JsonProperty("configFileId")]
        public string ConfigFileId { get; set; }
        [JsonProperty("configFile")]
        public FileEntity ConfigFile { get; set; }
        [JsonProperty("modelFileId")]
        public string ModelFileId { get; set; }
        [JsonProperty("modelFile")]
        public FileEntity ModelFile { get; set; }
        [JsonProperty("dictFileId")]
        public string DictFileId { get; set; }
        [JsonProperty("dictFile")]
        public FileEntity DictFile { get; set; }
        //public List<QuarterTrim> QuarterTrims { get; set; }
        public DateTimeOffset? ModifiedDate { get; set; }
    }
}
