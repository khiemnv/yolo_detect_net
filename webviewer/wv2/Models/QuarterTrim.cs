using Repositories;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models
{
    public class QuarterTrim : IEntity
    {
        [NotMapped]
        public int? No { get; set; }
        public string Id { get; set; } = RepositoryHelper.NewId();
        public string DetectModelId { get; set; }
        public string Barcode { get; set; } //debug
        public IEnumerable<Panel> Panels { get; set; }
        public bool? Judge { get; set; }
        public bool? Fixed { get; set; }
        public DateTimeOffset? CreatedDate { get; set; } = DateTimeOffset.Now;
    }
}
