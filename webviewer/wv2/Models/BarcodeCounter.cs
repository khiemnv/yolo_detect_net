using Repositories;

namespace Models
{
    public class BarcodeCounter : IEntity
    {
        public string Id { get; set; } = RepositoryHelper.NewId();

        public string Barcode { get; set; } = "";

        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
    }
}
