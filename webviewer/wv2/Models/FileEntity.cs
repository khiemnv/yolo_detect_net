using Newtonsoft.Json;
using Repositories;

namespace Models
{
    public class FileEntity : IEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; } = RepositoryHelper.NewId();
        [JsonProperty("path")]
        public string Path { get; set; } = null;
        public FileEntity(string path) { this.Path = path; }
        public FileEntity()
        {
            Path = null;
        }
        public static String GetBase64(string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    var buffer = System.IO.File.ReadAllBytes(file);
                    var base64 = Convert.ToBase64String(buffer);
                    return base64;
                }
                else { return null; }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return null;
            }
        }
    }

}
