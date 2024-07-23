using Repositories;

namespace Models
{
    public class Notification : IEntity
    {
        public string Id { get; set; } = RepositoryHelper.NewId();
        public string Title { get; set; }
        public string Description { get; set; }
    }
}
